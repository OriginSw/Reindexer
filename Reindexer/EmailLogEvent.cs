using System;

public class EmailLogEvent
{
    public DateTime CreatedOn { get; set; }

    public EmailLogEventType Type { get; set; }

    public string Notes { get; set; }
}