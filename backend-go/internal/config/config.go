package config

import (
	"os"
)

type Config struct {
	RabbitMQURL string
	HTTPPort    string
}

func Load() *Config {
	rabbitURL := os.Getenv("RABBITMQ_URL")
	if rabbitURL == "" {
		rabbitURL = "amqp://guest:guest@localhost:5672"
	}

	httpPort := os.Getenv("HTTP_PORT")
	if httpPort == "" {
		httpPort = "8080"
	}

	return &Config{
		RabbitMQURL: rabbitURL,
		HTTPPort:    httpPort,
	}
}
