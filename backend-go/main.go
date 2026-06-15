package main

import (
	"log"
	"os"
	"os/signal"
	"sync"
	"syscall"

	"github.com/LuisTerranova/invoices-app/backend-go/internal/imaging"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/messaging"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/ocr"
	"github.com/LuisTerranova/invoices-app/backend-go/internal/parser"
	amqp "github.com/rabbitmq/amqp091-go"
)

func main() {
	rabbitURL := os.Getenv("RABBITMQ_URL")
	if rabbitURL == "" {
		rabbitURL = "amqp://guest:guest@localhost:5672"
	}

	conn, err := amqp.Dial(rabbitURL)
	if err != nil {
		log.Fatalf("Failed to connect to RabbitMQ: %v", err)
	}
	defer conn.Close()

	controlCh, err := conn.Channel()
	if err != nil {
		log.Fatalf("Failed to open control channel: %v", err)
	}
	defer controlCh.Close()

	q, err := controlCh.QueueDeclare("invoices_to_process", true, false, false, false, nil)
	if err != nil {
		log.Fatalf("Failed to declare queue: %v", err)
	}

	msgs, err := controlCh.Consume(q.Name, "", false, false, false, false, nil)
	if err != nil {
		log.Fatalf("Failed to register consumer: %v", err)
	}

	log.Println("[*] Awaiting invoices. Press CTRL+C to stop process")

	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)

	go func() {
		<-sigCh
		log.Println("Shutting down...")
		controlCh.Cancel(q.Name, false)
	}()

	sem := make(chan struct{}, 10)
	var wg sync.WaitGroup

	for d := range msgs {
		sem <- struct{}{}

		wg.Add(1)
		go func(delivery amqp.Delivery) {
			defer wg.Done()
			defer func() { <-sem }()

			workerCh, err := conn.Channel()
			if err != nil {
				log.Printf("Failed to open worker channel: %v", err)
				delivery.Nack(false, true)
				return
			}
			defer workerCh.Close()

			raw, err := messaging.ToRawInvoice(delivery.Body)
			if err != nil {
				log.Printf("Error unmarshaling invoice: %v", err)
				delivery.Nack(false, true)
				return
			}

			processedImage, procErr := imaging.PrepareForOCR(raw.ImageData)
			if procErr != nil {
				log.Printf("Image processing error: %v", procErr)
				delivery.Nack(false, true)
				return
			}

			extractedText, ocrErr := ocr.ExtractText(processedImage)
			if ocrErr != nil {
				log.Printf("OCR error: %v", ocrErr)
				delivery.Nack(false, true)
				return
			}

			result := parser.Parse(extractedText, raw.ID)

			if err := messaging.PublishParsedInvoice(workerCh, result); err != nil {
				log.Printf("Failed to publish parsed invoice %s: %v", result.ID, err)
				delivery.Nack(false, true)
				return
			}

			delivery.Ack(false)
		}(d)
	}

	wg.Wait()
	log.Println("All workers finished. Exiting.")
}
