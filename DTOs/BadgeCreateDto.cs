namespace BadgeCraft_Net.DTOs
{
    public class BadgeCreateDto
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string BgColor { get; set; }
        public string TextColor { get; set; }

        public int OrganizationId { get; set; }

        public IFormFile Logo { get; set; }

    }
}