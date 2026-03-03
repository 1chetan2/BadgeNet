namespace BadgeCraft_Net.DTOs
{
    public class BadgeTemplateFieldDto
    {
        public string Type { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }

        public string? StyleJson { get; set; }
        public bool IsRequired { get; set; }
        public string? DefaultValue { get; set; }
    }
}