package main

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"github.com/LuisTerranova/invoices-app/backend-go/internal/config"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/imaging"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/messaging"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/ocr"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/parser"
	amqp "github.com/rabbitmq/amqp091-go"
)

func main() {
	// 1. Setup Structured Logging
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	// 2. Load Configuration
	cfg := config.Load()

	// 3. Setup Context for Graceful Shutdown
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)

	// 4. Start Health Check Server
	mux := http.NewServeMux()
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte("OK"))
	})
	httpServer := &http.Server{
		Addr:    ":" + cfg.HTTPPort,
		Handler: mux,
	}

	go func() {
		slog.Info("Starting health check server", "port", cfg.HTTPPort)
		if err := httpServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			slog.Error("Health check server failed", "error", err)
		}
	}()

	// 5. Connect to RabbitMQ
	conn, err := amqp.Dial(cfg.RabbitMQURL)
	if err != nil {
		slog.Error("Failed to connect to RabbitMQ", "error", err)
		os.Exit(1)
	}
	defer conn.Close()

	controlCh, err := conn.Channel()
	if err != nil {
		slog.Error("Failed to open control channel", "error", err)
		os.Exit(1)
	}
	defer controlCh.Close()

	q, err := controlCh.QueueDeclare("invoices_to_process", true, false, false, false, nil)
	if err != nil {
		slog.Error("Failed to declare queue", "error", err)
		os.Exit(1)
	}

	msgs, err := controlCh.Consume(q.Name, "go_worker", false, false, false, false, nil)
	if err != nil {
		slog.Error("Failed to register consumer", "error", err)
		os.Exit(1)
	}

	slog.Info("[*] Awaiting invoices. Press CTRL+C to stop process")

	go func() {
		<-sigCh
		slog.Info("Shutting down gracefully...")
		cancel()
		controlCh.Cancel("go_worker", false)

		// Shutdown HTTP server
		shutdownCtx, shutdownCancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer shutdownCancel()
		httpServer.Shutdown(shutdownCtx)
	}()

	sem := make(chan struct{}, 10)
	var wg sync.WaitGroup

	for {
		select {
		case <-ctx.Done():
			slog.Info("Context cancelled, stopping consumer loop")
			goto Cleanup
		case d, ok := <-msgs:
			if !ok {
				goto Cleanup
			}
			sem <- struct{}{}
			wg.Add(1)

			go func(delivery amqp.Delivery) {
				defer wg.Done()
				defer func() { <-sem }()

				// Process invoice
				processInvoice(ctx, conn, delivery)
			}(d)
		}
	}

Cleanup:
	wg.Wait()
	slog.Info("All workers finished. Exiting.")
}

func processInvoice(ctx context.Context, conn *amqp.Connection, delivery amqp.Delivery) {
	workerCh, err := conn.Channel()
	if err != nil {
		slog.Error("Failed to open worker channel", "error", err)
		delivery.Nack(false, true)
		return
	}
	defer workerCh.Close()

	raw, err := messaging.ToRawInvoice(delivery.Body)
	if err != nil {
		slog.Error("Error unmarshaling invoice", "error", err)
		delivery.Nack(false, true) // Reject and requeue, or better reject without requeue if invalid format
		return
	}

	logger := slog.With("invoice_id", raw.ID)
	logger.Info("Processing invoice")

	// Preprocess image
	processedImage, procErr := imaging.PrepareForOCR(raw.ImageData)
	if procErr != nil {
		logger.Error("Image processing error", "error", procErr)
		delivery.Nack(false, true)
		return
	}

	// Extract text using OCR
	// Note: Tesseract call blocks, but we pass ctx mentally for scope
	extractedText, ocrErr := ocr.ExtractText(processedImage)
	if ocrErr != nil {
		logger.Error("OCR error", "error", ocrErr)
		delivery.Nack(false, true)
		return
	}

	// Parse text
	result := parser.Parse(extractedText, raw.ID)

	// Publish result
	if err := messaging.PublishParsedInvoice(ctx, workerCh, result); err != nil {
		logger.Error("Failed to publish parsed invoice", "error", err)
		delivery.Nack(false, true)
		return
	}

	delivery.Ack(false)
	logger.Info("Invoice processed successfully")
}
