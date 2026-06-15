package models

import (
	"strings"
	"time"

	"github.com/google/uuid"
)

type RawInvoice struct {
	ID        uuid.UUID `json:"id"`
	ImageData []byte    `json:"image_data"`
	CreatedAt time.Time `json:"created_at"`
}

type ParsedInvoice struct {
	ID            uuid.UUID    `json:"id"`
	RawID         uuid.UUID    `json:"raw_id"`
	RawText       *string      `json:"raw_text"`
	AccessKey     *string      `json:"access_key"`
	Establishment *string      `json:"establishment"`
	CNPJ          *string      `json:"cnpj"`
	Date          *time.Time   `json:"date"`
	Total         *float64     `json:"total"`
	Items         []ParsedItem `json:"items"`
	ParserVersion string       `json:"parser_version"`
	IsValid       bool         `json:"is_valid"`
	ParseErrors   []string     `json:"parse_errors"`
}

type ParsedItem struct {
	Name      *string  `json:"name"`
	Quantity  *int     `json:"quantity"`
	UnitPrice *float64 `json:"unit_price"`
	Total     *float64 `json:"total"`
}

var (
	cnpjFirstWeight  = [12]int{5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2}
	cnpjSecondWeight = [13]int{6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2}
	keyWeights       = [43]int{2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4}
)

func ValidateCNPJChecksum(cnpj string) bool {
	if len(cnpj) != 14 {
		return false
	}

	// Reject all-zero (passes mod-11 trivially)
	if cnpj == "00000000000000" {
		return false
	}

	digits := make([]int, 14)
	for i := 0; i < 14; i++ {
		d := cnpj[i] - '0'
		if d < 0 || d > 9 {
			return false
		}
		digits[i] = int(d)
	}

	sum := 0
	for i := 0; i < 12; i++ {
		sum += digits[i] * cnpjFirstWeight[i]
	}
	rem := sum % 11
	first := 0
	if rem >= 2 {
		first = 11 - rem
	}
	if first != digits[12] {
		return false
	}

	sum = 0
	for i := 0; i < 13; i++ {
		sum += digits[i] * cnpjSecondWeight[i]
	}
	rem = sum % 11
	second := 0
	if rem >= 2 {
		second = 11 - rem
	}

	return second == digits[13]
}

func ValidateAccessKeyChecksum(key string) bool {
	if len(key) != 44 {
		return false
	}

	digits := make([]int, 44)
	for i := 0; i < 44; i++ {
		d := key[i] - '0'
		if d < 0 || d > 9 {
			return false
		}
		digits[i] = int(d)
	}

	sum := 0
	for i := 0; i < 43; i++ {
		sum += digits[i] * keyWeights[i]
	}
	rem := sum % 11
	check := 0
	if rem >= 2 {
		check = 11 - rem
	}

	return check == digits[43]
}

func CleanCNPJ(cnpj string) string {
	return strings.NewReplacer(".", "", "/", "", "-", "").Replace(cnpj)
}

func (p *ParsedInvoice) Validate() {
	p.IsValid = true
	p.ParseErrors = make([]string, 0)

	if p.CNPJ == nil {
		p.IsValid = false
		p.ParseErrors = append(p.ParseErrors, "CNPJ not identified")
	} else {
		clean := CleanCNPJ(*p.CNPJ)
		if !ValidateCNPJChecksum(clean) {
			p.IsValid = false
			p.ParseErrors = append(p.ParseErrors, "CNPJ failed checksum validation")
		}
	}

	if len(p.Items) == 0 {
		p.IsValid = false
		p.ParseErrors = append(p.ParseErrors, "Invoice items not identified")
	}

	if p.Total == nil || *p.Total < 0 {
		p.IsValid = false
		p.ParseErrors = append(p.ParseErrors, "Total price not identified")
	}

	if p.Date == nil {
		p.IsValid = false
		p.ParseErrors = append(p.ParseErrors, "Invoice date not identified")
	}

	if p.AccessKey != nil && len(*p.AccessKey) == 44 {
		if !ValidateAccessKeyChecksum(*p.AccessKey) {
			p.IsValid = false
			p.ParseErrors = append(p.ParseErrors, "Access key failed checksum validation")
		}
	}
}
