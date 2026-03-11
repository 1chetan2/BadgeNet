namespace BadgeCraft_Net.DTOs
{
    public class UpdateUserDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsGranted { get; set; }
    }
}
