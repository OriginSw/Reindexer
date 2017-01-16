public class EmailLogEventType
{
    public enum Options
    {
        Requested = 1,
        Scheduled = 2,
        Delivered = 3,
        Opened = 4,
        Bounced = 5,
        Dropped = 6,
        Complained = 7,
        Unsubscribed = 8,
        Clicked = 9,
        SendFailed = 10,
        Deleted = 11
    }

    public short ID { get; set; }

    public string Name { get; set; }
}