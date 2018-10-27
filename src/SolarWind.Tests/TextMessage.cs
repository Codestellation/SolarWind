namespace Codestellation.SolarWind.Tests
{
    public class TextMessage
    {
        public string Text { get; set; }


        public static TextMessage New(string text = null) => new TextMessage
        {
            Text = text ?? "Tom got a small piece of pie."
        };
    }
}