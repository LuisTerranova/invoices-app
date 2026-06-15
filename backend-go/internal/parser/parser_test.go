package parser

import (
	"fmt"
	"testing"
	"time"

	"github.com/google/uuid"

	"github.com/LuisTerranova/invoices-app/backend-go/internal/models"
)

func TestParse_FullReceipt(t *testing.T) {
	raw := `MERCADO LIVRE
CNPJ: 60.570.781/0001-81
NFC-e 000123456
15/04/2025

1.500 ARROZ TIO JOAO 5KG X 23,90 35,85
2 UN LEITE CONDENSADO X 8,50 17,00
7891234567890 0,500 KG 12,00 6,00
3 X 5,50 16,50

VALOR TOTAL R$ 75,35`

	result := Parse(raw, uuid.Nil)

	if result.CNPJ == nil || *result.CNPJ != "60.570.781/0001-81" {
		t.Errorf("expected CNPJ 60.570.781/0001-81, got %v", result.CNPJ)
	}
	if result.Establishment == nil || *result.Establishment != "MERCADO LIVRE" {
		t.Errorf("expected establishment MERCADO LIVRE, got %v", result.Establishment)
	}
	if result.Date == nil || result.Date.Year() != 2025 || result.Date.Month() != 4 || result.Date.Day() != 15 {
		t.Errorf("expected date 2025-04-15, got %v", result.Date)
	}
	if result.Total == nil || *result.Total != 75.35 {
		t.Errorf("expected total 75.35, got %v", result.Total)
	}
	if len(result.Items) != 4 {
		t.Fatalf("expected 4 items, got %d", len(result.Items))
	}

	checkItem(t, result.Items[0], "ARROZ TIO JOAO 5KG", nil, f64p(23.90), f64p(35.85))
	checkItem(t, result.Items[3], "<any>", ip(3), f64p(5.50), f64p(16.50))
}

func TestParse_InvalidCNPJ_FallsBackToFormatMatch(t *testing.T) {
	raw := "LOJA TESTE\n12.345.678/0001-00\n15/04/2025\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.CNPJ == nil {
		t.Error("expected CNPJ even with invalid checksum, got nil")
	}
}

func TestParse_SubtotalFallback(t *testing.T) {
	raw := "LOJA X\n99.999.999/0001-91\n15/04/2025\n\n1 X 10,00\n\nSUBTOTAL R$ 10,00\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Total == nil || *result.Total != 10.00 {
		t.Errorf("expected total 10.00, got %v", result.Total)
	}
}

func TestParse_SubtotalOnly(t *testing.T) {
	raw := "LOJA Y\n99.999.999/0001-91\n15/04/2025\n\n1 X 10,00\n\nSUBTOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Total == nil || *result.Total != 10.00 {
		t.Errorf("expected total 10.00 from SUBTOTAL fallback, got %v", result.Total)
	}
}

func TestParse_AccessKeyWithoutKeyword(t *testing.T) {
	raw := "LOJA Z\n99.999.999/0001-91\n15/04/2025\n35200560705781000140550012345678901234567890\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.AccessKey != nil {
		t.Logf("access key found: %s", *result.AccessKey)
	}
}

func TestParse_InvalidAccessKey_ReturnsNil(t *testing.T) {
	raw := "LOJA Z\n99.999.999/0001-91\n15/04/2025\nCHAVE 123456789012345678901234567890\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.AccessKey != nil {
		t.Errorf("expected nil access key for truncated input, got %s", *result.AccessKey)
	}
}

func TestParse_StoreNameAfterHeaderKeywords(t *testing.T) {
	raw := "DANFE NFC-E\nDOCUMENTO AUXILIAR\nMERCADAO DO POVO\nCNPJ: 99.999.999/0001-91\nRUA ALGUMA, 200\n15/04/2025\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Establishment == nil || *result.Establishment != "MERCADAO DO POVO" {
		t.Errorf("expected establishment MERCADAO DO POVO, got %q", strOrNil(result.Establishment))
	}
}

func TestParse_StoreNameFromCNPJLine(t *testing.T) {
	raw := "15/04/2025\nCNPJ: 99.999.999/0001-91 NOME DA LOJA LTDA\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Establishment == nil || *result.Establishment != "NOME DA LOJA LTDA" {
		t.Errorf("expected establishment NOME DA LOJA LTDA, got %q", strOrNil(result.Establishment))
	}
}

func TestParse_EmptyText(t *testing.T) {
	result := Parse("", uuid.Nil)
	if result.IsValid {
		t.Error("expected IsValid=false for empty text")
	}
	if len(result.Items) != 0 {
		t.Errorf("expected 0 items, got %d", len(result.Items))
	}
}

func TestParse_DateISOFormat(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n2025/04/15\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Date == nil || result.Date.Year() != 2025 || result.Date.Month() != 4 || result.Date.Day() != 15 {
		t.Errorf("expected date 2025-04-15, got %v", result.Date)
	}
}

func TestParse_DateTwoDigitYear(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n15/04/25\n\n1 X 10,00\n\nVALOR TOTAL R$ 10,00"
	result := Parse(raw, uuid.Nil)
	if result.Date == nil || result.Date.Year() != 2025 || result.Date.Month() != 4 || result.Date.Day() != 15 {
		t.Errorf("expected date 2025-04-15, got %v", result.Date)
	}
}

func TestParse_ZeroTotal_IsValid(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n15/04/2025\n\n1 X 0,00\n\nVALOR TOTAL R$ 0,00"
	result := Parse(raw, uuid.Nil)
	if !result.IsValid {
		t.Errorf("expected IsValid=true for zero total")
	}
}

func TestParse_NegativeTotal_IsInvalid(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n15/04/2025\n\n1 X 10,00\n\nVALOR TOTAL R$ -10,00"
	result := Parse(raw, uuid.Nil)
	if result.IsValid {
		t.Errorf("expected IsValid=false for negative total")
	}
}

func TestParse_NextLineItemName(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n15/04/2025\n\n7891234567890 1,00 KG 12,50 12,50\nARROZ TIO JOAO\n\nVALOR TOTAL R$ 12,50"
	result := Parse(raw, uuid.Nil)
	t.Logf("Items: %d", len(result.Items))
	for i, item := range result.Items {
		q, p, tot := "<nil>", "<nil>", "<nil>"
		if item.Quantity != nil { q = fmt.Sprintf("%d", *item.Quantity) }
		if item.UnitPrice != nil { p = fmt.Sprintf("%.2f", *item.UnitPrice) }
		if item.Total != nil { tot = fmt.Sprintf("%.2f", *item.Total) }
		t.Logf("  [%d] name=%q qty=%s price=%s total=%s", i, strOrNil(item.Name), q, p, tot)
	}
	// Detailed debug: check if reDateDMY is matching the name
	name := "ARROZ TIO JOAO"
	t.Logf("reDateDMY.MatchString(%q) = %v", name, reDateDMY.MatchString(name))
	t.Logf("reBarcode.MatchString(%q) = %v", name, reBarcode.MatchString(name))
	t.Logf("len(name) > 0 = %v", len(name) > 0)
	if len(result.Items) != 1 {
		t.Fatalf("expected 1 item, got %d", len(result.Items))
	}
	if result.Items[0].Name == nil || *result.Items[0].Name != "ARROZ TIO JOAO" {
		t.Errorf("expected ARROZ TIO JOAO, got %v", result.Items[0].Name)
	}
}

func TestParse_MinimalItemFormat(t *testing.T) {
	raw := "LOJA\n99.999.999/0001-91\n15/04/2025\n\nLEITE\n2 X 8,50\n\nVALOR TOTAL R$ 17,00"
	result := Parse(raw, uuid.Nil)
	if len(result.Items) != 1 {
		t.Fatalf("expected 1 item, got %d", len(result.Items))
	}
	checkItem(t, result.Items[0], "LEITE", ip(2), f64p(8.50), f64p(17.00))
}

func TestCleanOCRText(t *testing.T) {
	tests := []struct {
		name, input, expected string
	}{
		{"trims leading empty", "\n\n\nLOJA\nITEM\n", "LOJA\nITEM"},
		{"preserves inline empty", "LOJA\n\nITEM\n", "LOJA\n\nITEM"},
		{"trims trailing empty", "LOJA\nITEM\n\n\n", "LOJA\nITEM"},
		{"collapses spaces", "LOJA   TESTE\nITEM   123", "LOJA TESTE\nITEM 123"},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := cleanOCRText(tt.input); got != tt.expected {
				t.Errorf("got %q, want %q", got, tt.expected)
			}
		})
	}
}

func TestParseBrazilianFloat(t *testing.T) {
	tests := []struct {
		input string
		want  float64
	}{
		{"10,50", 10.50},
		{"1.234,56", 1234.56},
		{"1.234.567,89", 1234567.89},
		{"1234.56", 1234.56},
		{"0,00", 0},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			if got := parseBrazilianFloat(tt.input); got == nil || *got != tt.want {
				t.Errorf("got %v, want %v", got, tt.want)
			}
		})
	}
}

func TestExtractInvoiceDate(t *testing.T) {
	tests := []struct {
		input string
		want  time.Time
	}{
		{"15/04/2025", time.Date(2025, 4, 15, 0, 0, 0, 0, time.UTC)},
		{"15-04-2025", time.Date(2025, 4, 15, 0, 0, 0, 0, time.UTC)},
		{"2025-04-15", time.Date(2025, 4, 15, 0, 0, 0, 0, time.UTC)},
		{"15/04/25", time.Date(2025, 4, 15, 0, 0, 0, 0, time.UTC)},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			if got := extractInvoiceDate(tt.input); got == nil || !got.Equal(tt.want) {
				t.Errorf("got %v, want %v", got, tt.want)
			}
		})
	}
}

func TestValidateCNPJChecksum(t *testing.T) {
	tests := []struct {
		cnpj string
		want bool
	}{
		{"60570781000140", false},
		{"12345678000100", false},
		{"00000000000000", false},
		{"", false},
		{"123", false},
	}
	for _, tt := range tests {
		t.Run(tt.cnpj, func(t *testing.T) {
			if got := models.ValidateCNPJChecksum(tt.cnpj); got != tt.want {
				t.Errorf("got %v, want %v", got, tt.want)
			}
		})
	}
}

func TestCNPJ998IsValid(t *testing.T) {
	if !models.ValidateCNPJChecksum("99999999000191") {
		t.Error("99.999.999/0001-91 should be valid")
	}
}

func TestCNPJ607IsValid(t *testing.T) {
	if !models.ValidateCNPJChecksum("60570781000181") {
		t.Error("60.570.781/0001-81 should be valid")
	}
}

// helpers
func strOrNil(s *string) string {
	if s == nil { return "<nil>" }
	return *s
}

func ip(v int) *int { return &v }
func f64p(v float64) *float64 { return &v }

func checkItem(t *testing.T, item models.ParsedItem, name string, qty *int, price, total *float64) {
	t.Helper()
	if name != "<any>" {
		if item.Name == nil || *item.Name != name {
			t.Errorf("name: got %q, want %q", strOrNil(item.Name), name)
		}
	}
	if qty != nil {
		if item.Quantity == nil || *item.Quantity != *qty {
			t.Errorf("qty: got %v, want %v", item.Quantity, qty)
		}
	}
	if price != nil {
		if item.UnitPrice == nil || *item.UnitPrice != *price {
			t.Errorf("price: got %v, want %v", item.UnitPrice, price)
		}
	}
	if total != nil {
		if item.Total == nil || *item.Total != *total {
			t.Errorf("total: got %v, want %v", item.Total, total)
		}
	}
}
