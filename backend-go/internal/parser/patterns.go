package parser

import "regexp"

var (

	// Capture 44-digit access key (handles OCR spaces)
	reAccessKey = regexp.MustCompile(`(?i)(?:CHAVE|CONSULTA)\D*(\d[\s\d]{43,60})`)

	// Standalone 44-digit sequence when no keyword is found (handles OCR spaces)
	reAccessKeyStandalone = regexp.MustCompile(`(\d[\s\d]{43,60})`)

	// Match standard Brazilian CNPJ format
	reCNPJ = regexp.MustCompile(`\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}`)

	// Extract date in DD/MM/YYYY, DD-MM-YYYY, or DD.MM.YYYY format
	reDate = regexp.MustCompile(`\d{2}[/\-\.]\d{2}[/\-\.]\d{4}`)

	// Match final amount due (Brazilian invoice keywords) — excludes SUBTOTAL
	reTotalAmount = regexp.MustCompile(`(?i)(?:VALOR\s+(?:A\s+PAGAR|TOTAL|LIQUIDO)|TOTAL(?:\s+(?:GERAL|DA\s+NOTA|A\s+PAGAR))?)\s*R?\$?\s*([\d\s,.]+)`)

	// SUBTOTAL only — fallback if final total is missing
	reSubtotal = regexp.MustCompile(`(?i)SUBTOTAL\s*R?\$?\s*([\d\s,.]+)`)

	// Item patterns tried in order — first match on each line wins
	itemPatterns = []*regexp.Regexp{
		// EAN-prefixed format with optional qty: "789... 0,280 KG 40,40 11,07" or "789... KG 40,40 11,07"
		regexp.MustCompile(`\b\d{8,14}\b\s+(?:(?P<qty>[\d,.]+)\s+)?(?P<unit>\S{2,5})\s+(?P<unit_price>[\d,.]+)\s+(?P<total_item_raw>.+)$`),
		// Full format with unit code (no EAN prefix): "1,000 UN X 10,99 10,99"
		regexp.MustCompile(`(?P<qty>[\d,.]+)\s+(?P<unit>UN|KG|L|UNID|PC|PCT|CX|LT|GR|ML|FD|KIT|M|M2|M3|SC|FR|PAR|PÇ|RL|TB|GL|CP|BD|CJ|PACOTE|LATA|CART|TER)\s*(?:X\s*)?(?P<unit_price>[\d\s,.]+)\s+(?P<total_item_raw>.+)$`),
		// Simplified format: "2 X 10,99 21,98" (no unit code)
		regexp.MustCompile(`(?P<qty>[\d,.]+)\s*X\s*(?P<unit_price>[\d,.]+)\s+(?P<total_item_raw>.+)$`),
		// Minimal format: "2 X 10,99" (qty × unit price, no total)
		regexp.MustCompile(`(?P<qty>[\d,.]+)\s*X\s*(?P<unit_price>[\d,.]+)$`),
	}

	// Identify store names/headers (anchored to start of line)
	reStoreName = regexp.MustCompile(`(?i)^[A-Z][A-Z\s]*?(?:LOJA|DISTRIBUIDORA|MERCADO|FARMACIA|POSTO|MERCEARIA|PADARIA|ACOUGUE|SUPERMERCADO|COMERCIAL|EMPRESA)`)
)
