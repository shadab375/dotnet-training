namespace Models {
    public class Todo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Completed { get; set; } = false;
        public string UserId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Deadline { get; set; }
        public string Priority { get; set; } = "Medium";
    }
} 