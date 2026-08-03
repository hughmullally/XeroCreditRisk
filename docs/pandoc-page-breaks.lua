-- Inserts a native Word page break before specific H2 headings when
-- converting XERO_CREDIT_RISK_DOCUMENTATION.md to .docx via pandoc.
-- Usage: pandoc ... --lua-filter=pandoc-page-breaks.lua
-- Has no effect on non-docx output formats (raw openxml blocks are dropped).

local targets = {
  ["Status and Improvement Strategy"] = true,
  ["Market Research"] = true,
  ["Appendix A: Overview"] = true,
  ["Appendix B: Features"] = true,
  ["Appendix C: Architecture"] = true,
}

function Pandoc(doc)
  local newBlocks = {}
  for _, block in ipairs(doc.blocks) do
    if block.t == "Header" and block.level == 2 then
      local text = pandoc.utils.stringify(block.content)
      if targets[text] then
        table.insert(newBlocks, pandoc.RawBlock('openxml', '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'))
      end
    end
    table.insert(newBlocks, block)
  end
  doc.blocks = newBlocks
  return doc
end
