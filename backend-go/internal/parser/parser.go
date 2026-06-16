package parser

import (
	"math"
	"regexp"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"

	"github.com/LuisTerranova/invoices-app/backend-go/internal/models"
)

var (
	reMultipleSpaces = regexp.MustCompile(`[ \t]+`)
	reBarcode        = regexp.MustCompile(`\d{8,}`)
	rePrice          = regexp.MustCompile(`[\d]+[,.]\d{2}`)
	reNumber         = regexp.MustCompile(`[\d,.]+`)
	reDateDMY        = regexp.MustCompile(`^\d{2}[/\-\.]\d{2}[/\-\.]\d{2,4}$`)
	reTrailingUnit   = regexp.MustCompile(`(?i)\s+(UN|KG|L|UNID|PC|PCT|CX|LT|GR|ML|FD|KIT|M|M2|M3|SC|FR|PAR|PÇ|RL|TB|GL|CP|BD|CJ)\s*$`)
	reTrailingX      = regexp.MustCompile(`\s*X\s*$`)
)

func Parse(rawText string, rawID uuid.UUID) models.ParsedInvoice {
	text := cleanOCRText(rawText)
	upperText := strings.ToUpper(text)
	lines := strings.Split(text, "\n")

	parsed := models.ParsedInvoice{
		ID:            uuid.New(),
		RawID:         rawID,
		RawText:       &text,
		AccessKey:     extractAccessKey(upperText),
		CNPJ:          extractCNPJ(upperText),
		Establishment: extractStoreName(lines),
		Date:          extractInvoiceDate(upperText),
		Items:         parseItems(lines, upperText),
		Total:         extractTotal(upperText),
		ParserVersion: "1.1.0",
	}

	parsed.Validate()

	return parsed
}

func extractAccessKey(text string) *string {
	match := reAccessKey.FindStringSubmatch(text)
	if len(match) > 1 {
		cleanKey := cleanAccessKey(match[1])
		if models.ValidateAccessKeyChecksum(cleanKey) {
			return &cleanKey
		}
	}

	match = reAccessKeyStandalone.FindStringSubmatch(text)
	for idx, m := range match {
		if idx == 0 || m == "" {
			continue
		}
		cleanKey := cleanAccessKey(m)
		if models.ValidateAccessKeyChecksum(cleanKey) {
			return &cleanKey
		}
	}

	return nil
}

func cleanAccessKey(key string) string {
	return strings.TrimSpace(strings.NewReplacer(" ", "", "\t", "", "\n", "").Replace(key))
}

func extractStoreName(lines []string) *string {
	itemStart := findItemBlockStart(lines)

	searchLimit := itemStart
	if searchLimit > 20 {
		searchLimit = 20
	}

	// Track first meaningful non-address line as fallback
	var firstNameLine *string

	for i := 0; i < searchLimit; i++ {
		line := strings.TrimSpace(lines[i])
		upper := strings.ToUpper(line)
		if line == "" {
			continue
		}

		if reBarcode.MatchString(upper) ||
			strings.Contains(upper, "CHAVE") ||
			strings.Contains(upper, "CONSULTA") ||
			strings.Contains(upper, "DANFE") ||
			strings.Contains(upper, "NFE") ||
			strings.Contains(upper, "NFC-E") ||
			strings.Contains(upper, "DOCUMENTO AUXILIAR") {
			continue
		}

		if reStoreName.MatchString(upper) {
			return &line
		}

		// Fallback: capture first non-empty line within first 5 that isn't address-like
		if firstNameLine == nil && i < 5 && len(line) > 5 &&
			!reCNPJ.MatchString(upper) &&
			!strings.Contains(upper, "RUA") &&
			!strings.Contains(upper, "AV ") &&
			!strings.Contains(upper, "AVENIDA") {
			firstNameLine = &line
		}

		if reCNPJ.MatchString(upper) {
			nameOnly := reCNPJ.ReplaceAllString(line, "")
			nameOnly = strings.ReplaceAll(nameOnly, "CNPJ:", "")
			nameOnly = strings.ReplaceAll(nameOnly, "cnpj:", "")
			nameOnly = strings.TrimSpace(nameOnly)
			if len(nameOnly) > 3 {
				return &nameOnly
			}
			continue
		}

		if i < 3 && len(line) < 80 && !strings.Contains(upper, "NFE") && !strings.Contains(upper, "NFC-E") && !strings.Contains(upper, "DANFE") && !reDateDDMMYYYY.MatchString(upper) && !reDateYYYYMMDD.MatchString(upper) {
			return &line
		}
	}

	// If regex found nothing, return first captured line as fallback
	if firstNameLine != nil {
		return firstNameLine
	}

	return nil
}

func findItemBlockStart(lines []string) int {
	for i, line := range lines {
		upper := strings.ToUpper(line)
		if reTotalAmount.MatchString(upper) || reSubtotal.MatchString(upper) {
			return i
		}
		for _, re := range itemPatterns {
			if re.MatchString(upper) {
				return i
			}
		}
	}
	return len(lines)
}

func extractCNPJ(text string) *string {
	matches := reCNPJ.FindAllString(text, -1)
	for _, match := range matches {
		clean := models.CleanCNPJ(match)
		if len(clean) == 14 && models.ValidateCNPJChecksum(clean) {
			return &match
		}
	}

	for _, match := range matches {
		clean := models.CleanCNPJ(match)
		if len(clean) == 14 {
			return &match
		}
	}

	return nil
}

var (
	reDateDDMMYYYY = regexp.MustCompile(`\d{2}[/\-\.]\d{2}[/\-\.]\d{4}`)
	reDateYYYYMMDD = regexp.MustCompile(`\d{4}[/\-\.]\d{2}[/\-\.]\d{2}`)
	reDateDDMMYY   = regexp.MustCompile(`\d{2}[/\-\.]\d{2}[/\-\.]\d{2}`)
)

func extractInvoiceDate(text string) *time.Time {
	var match string
	var layout string

	m := reDateDDMMYYYY.FindString(text)
	if m != "" {
		match = m
		layout = "02/01/2006"
	}

	if match == "" {
		m = reDateYYYYMMDD.FindString(text)
		if m != "" {
			match = m
			layout = "2006/01/02"
		}
	}

	if match == "" {
		m = reDateDDMMYY.FindString(text)
		if m != "" {
			match = m
			layout = "02/01/06"
		}
	}

	if match == "" {
		return nil
	}

	cleanDate := strings.ReplaceAll(match, " ", "")
	cleanDate = strings.ReplaceAll(cleanDate, "-", "/")
	cleanDate = strings.ReplaceAll(cleanDate, ".", "/")

	t, err := time.Parse(layout, cleanDate)
	if err != nil {
		return nil
	}
	return &t
}

func cleanOCRText(text string) string {
	text = reMultipleSpaces.ReplaceAllString(text, " ")

	var builder strings.Builder
	lines := strings.Split(text, "\n")
	var buf []string

	started := false
	for _, line := range lines {
		trimmed := strings.TrimSpace(line)
		if !started && trimmed == "" {
			continue
		}
		started = true
		buf = append(buf, trimmed)
	}

	for len(buf) > 0 && buf[len(buf)-1] == "" {
		buf = buf[:len(buf)-1]
	}

	for i, s := range buf {
		if i > 0 {
			builder.WriteByte('\n')
		}
		builder.WriteString(s)
	}

	return builder.String()
}

func parseItems(lines []string, upperText string) []models.ParsedItem {
	var items []models.ParsedItem
	upperLines := strings.Split(upperText, "\n")

	// Common non-item keywords in Brazilian invoices
	rejectKeywords := []string{
		"CNPJ", "NFC-E", "CHAVE", "CONSULTA", "DANFE",
		"RUA", "AV ", "AVENIDA", "BAIRRO", "CENTRO", "COMPLEMENTO",
		"CEP", "FONE", "TELEFONE", "IE ", "IMPRESSO",
		"ICMS", "PIS", "COFINS", "IPI", "FRETE", "SEGURO", "FCP",
		"DESCONTO", "TROCO", "FORMA PAG", "CARTAO", "DINHEIRO",
		"PAGAMENTO", "TOTAL", "SUBTOTAL", "VALOR",
		"QTD", "PRODUTO", "CÓDIGO", "CODIGO",
		"ST ", "FCP",
		"CAIXA", "OBSERVA", "OBS ",
	}

	for i, line := range lines {
		upper := upperLines[i]
		trimmed := strings.TrimSpace(line)

		// Skip empty, too short, or date-only lines
		if reDateDMY.MatchString(trimmed) || len(trimmed) < 4 {
			continue
		}

		// Skip lines starting with separators or known non-item chars
		if strings.HasPrefix(trimmed, "/") || strings.HasPrefix(trimmed, "-") ||
			strings.HasPrefix(trimmed, ":") || strings.HasPrefix(trimmed, ".") ||
			strings.HasPrefix(trimmed, "–") {
			continue
		}

		// Skip lines with time patterns (HH:MM or HH:MM:SS)
		if strings.Contains(upper, ":") {
			continue
		}

		// Skip lines with percentage sign
		if strings.Contains(upper, "%") {
			continue
		}

		// Skip lines containing known non-item keywords
		skip := false
		for _, kw := range rejectKeywords {
			if strings.Contains(upper, kw) {
				skip = true
				break
			}
		}
		if skip {
			continue
		}

		// Skip lines matching CNPJ format even without the label
		if reCNPJ.MatchString(upper) {
			continue
		}

		item := tryParseItemLine(line)
		if item == nil {
			item = parseFlexibleItemLine(line)
		}
		if item == nil {
			continue
		}

		// Reject items with unrealistically high totals (> R$ 25,000 per item)
		if item.Total != nil && *item.Total > 25000 {
			continue
		}

		if item.Name == nil && i > 0 {
			name := strings.TrimSpace(lines[i-1])
			if len(name) > 0 && !reBarcode.MatchString(name) && !rePrice.MatchString(name) &&
				!reDateDMY.MatchString(name) && !strings.Contains(strings.ToUpper(name), "CAIXA") {
				item.Name = &name
			}
		}

		if item.Name == nil && i < len(lines)-1 {
			name := strings.TrimSpace(lines[i+1])
			if len(name) > 0 && !reBarcode.MatchString(name) && !reDateDMY.MatchString(name) &&
				!strings.Contains(strings.ToUpper(name), "CAIXA") {
				item.Name = &name
			}
		}

		if item.Total == nil && item.Quantity != nil && item.UnitPrice != nil {
			computed := *item.UnitPrice * float64(*item.Quantity)
			item.Total = &computed
		}

		items = append(items, *item)
	}
	return items
}

func tryParseItemLine(line string) *models.ParsedItem {
	for _, re := range itemPatterns {
		matches := re.FindStringSubmatch(line)
		if len(matches) == 0 {
			continue
		}

		item := &models.ParsedItem{}
		groupNames := re.SubexpNames()

		matchPos := strings.Index(line, matches[0])
		if matchPos > 0 {
			name := strings.TrimSpace(line[:matchPos])
			if len(name) > 0 && !reBarcode.MatchString(name) {
				item.Name = &name
			}
		}

		for idx, name := range groupNames {
			if idx == 0 || idx >= len(matches) {
				continue
			}

			switch name {
			case "qty":
				if matches[idx] != "" {
					if v := parseBrazilianFloat(matches[idx]); v != nil {
						q := roundQty(*v)
						item.Quantity = &q
					}
				} else if item.Quantity == nil {
					q := 1
					item.Quantity = &q
				}
			case "unit_price":
				item.UnitPrice = parseBrazilianFloat(matches[idx])
			case "total_item":
				item.Total = parseBrazilianFloat(matches[idx])
			case "total_item_raw":
				item.Total = extractLastNumber(matches[idx])
			}
		}

		return item
	}
	return nil
}

func roundQty(v float64) int {
	if v < 1 && v > 0 {
		return 1
	}
	return int(math.Round(v))
}

func parseFlexibleItemLine(line string) *models.ParsedItem {
	upper := strings.ToUpper(line)
	if strings.Contains(upper, "RUA") || strings.Contains(upper, "AV ") ||
		strings.Contains(upper, "AVENIDA") || strings.Contains(upper, "BAIRRO") ||
		strings.Contains(upper, "CEP") || strings.Contains(upper, "FONE") ||
		strings.Contains(upper, "TELEFONE") || strings.Contains(upper, "CNPJ") ||
		strings.Contains(upper, "IE ") || strings.Contains(upper, "IMPRESSO") ||
		strings.Contains(upper, "ACESSO") || strings.Contains(upper, "CHAVE") ||
		strings.Contains(upper, "CONSULTA") || strings.Contains(upper, "NFC-E") ||
		strings.Contains(upper, "DANFE") || strings.Contains(upper, "VALOR") ||
		strings.Contains(upper, "TOTAL") || strings.Contains(upper, "SUBTOTAL") ||
		strings.Contains(upper, "DESCONTO") || strings.Contains(upper, "TROCO") ||
		strings.Contains(upper, "ICMS") || strings.Contains(upper, "PIS") ||
		strings.Contains(upper, "COFINS") || strings.Contains(upper, "IPI") ||
		strings.Contains(upper, "FRETE") || strings.Contains(upper, "SEGURO") ||
		strings.Contains(upper, "FORMA PAG") || strings.Contains(upper, "CARTAO") ||
		strings.Contains(upper, "DINHEIRO") || strings.Contains(upper, "PAGAMENTO") ||
		strings.Contains(upper, "FCP") || strings.Contains(upper, "ST ") ||
		strings.Contains(upper, "CENTRO") || strings.Contains(upper, "COMPLEMENTO") ||
		strings.Contains(upper, "MUNICIPIO") || reCNPJ.MatchString(upper) ||
		strings.Contains(upper, "%") {
		return nil
	}

	allNumbers := reNumber.FindAllString(line, -1)
	if len(allNumbers) < 3 || len(allNumbers) > 6 {
		return nil
	}

	// Reject lines that look like barcodes (many consecutive digits)
	var digitCount int
	for _, s := range allNumbers {
		clean := strings.NewReplacer(",", "", ".", "").Replace(s)
		if len(clean) >= 8 {
			digitCount++
		}
	}
	if digitCount >= 2 {
		return nil
	}

	values := make([]float64, 0, len(allNumbers))
	for _, s := range allNumbers {
		if v := parseBrazilianFloat(s); v != nil {
			values = append(values, *v)
		}
	}
	if len(values) < 2 {
		return nil
	}

	hasFractionalItem := false
	for _, v := range values {
		if v > 0 && v < 1 {
			hasFractionalItem = true
			break
		}
	}

	item := &models.ParsedItem{}
	item.Total = &values[len(values)-1]
	item.UnitPrice = &values[len(values)-2]

	if len(values) >= 3 {
		v := values[len(values)-3]
		if v > 0 && v <= 999 && v == float64(int(v)) {
			q := int(v)
			item.Quantity = &q
		} else if hasFractionalItem {
			q := 1
			item.Quantity = &q
		} else {
			q := 1
			item.Quantity = &q
		}
	} else {
		q := 1
		item.Quantity = &q
	}

	numPositions := reNumber.FindAllStringIndex(line, -1)
	if len(numPositions) >= 2 {
		nameStart := numPositions[0][1]
		nameEnd := numPositions[len(numPositions)-2][0]
		if nameStart < nameEnd {
			name := strings.TrimSpace(line[nameStart:nameEnd])
			name = reTrailingUnit.ReplaceAllString(name, "")
			name = reTrailingX.ReplaceAllString(name, "")
			name = strings.TrimSpace(name)
			if len(name) > 0 && !reBarcode.MatchString(name) {
				item.Name = &name
			}
		}
	}

	return item
}

func parseBrazilianFloat(s string) *float64 {
	clean := strings.ReplaceAll(s, " ", "")

	commaIdx := strings.Index(clean, ",")
	dotIdx := strings.Index(clean, ".")

	if commaIdx >= 0 {
		clean = strings.ReplaceAll(clean, ".", "")
		clean = strings.ReplaceAll(clean, ",", ".")
	} else if dotIdx >= 0 && strings.Count(clean, ".") == 1 {
	} else if dotIdx >= 0 {
		lastDot := strings.LastIndex(clean, ".")
		beforeLast := clean[:lastDot]
		afterLast := clean[lastDot:]
		beforeLast = strings.ReplaceAll(beforeLast, ".", "")
		clean = beforeLast + afterLast
	}

	value, err := strconv.ParseFloat(clean, 64)
	if err != nil {
		return nil
	}
	return &value
}

func extractLastNumber(s string) *float64 {
	matches := reNumber.FindAllString(s, -1)
	for i := len(matches) - 1; i >= 0; i-- {
		if v := parseBrazilianFloat(matches[i]); v != nil {
			return v
		}
	}
	return nil
}

func extractTotal(text string) *float64 {
	match := reTotalAmount.FindStringSubmatch(text)
	if len(match) > 1 {
		v := strings.TrimSpace(match[1])
		return parseBrazilianFloat(v)
	}

	match = reSubtotal.FindStringSubmatch(text)
	if len(match) > 1 {
		v := strings.TrimSpace(match[1])
		return parseBrazilianFloat(v)
	}

	return nil
}
