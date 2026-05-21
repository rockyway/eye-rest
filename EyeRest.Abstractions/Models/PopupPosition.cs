namespace EyeRest.Models
{
    /// <summary>
    /// Domain enum for popup placement preference. Maps to the UI-layer
    /// PopupPlacement enum at the notification service boundary.
    /// Persisted as integer to match the existing project convention.
    /// </summary>
    public enum PopupPosition
    {
        Center = 0,
        TopLeft = 1,
        TopCenter = 2,
        TopRight = 3,
        LeftCenter = 4,
        RightCenter = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,
    }
}
