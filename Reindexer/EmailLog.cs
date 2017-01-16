using Nest;
using System;
using System.Collections.Generic;

public class EmailLog
{
    [String(Ignore = true)]
    public string ID { get; set; }

    public App App { get; set; }

    public Contact Sender { get; set; }

    public Contact Recipient { get; set; }

    public EmailTemplate Template { get; set; }

    public EmailTemplateLocalization TemplateLocalization { get; set; }

    public string Data { get; set; }

    public bool IsTestEmail { get; set; }

    public DateTime RequestedOn { get; set; }

    public DateTime? ScheduledFor { get; set; }

    public DripCampaign DripCampaign { get; set; }

    public string JobID { get; set; }

    public EmailLogEventType LastEventType { get; set; }

    public ICollection<EmailLogEvent> Events { get; set; }

    public EmailLog()
    {
        Events = new HashSet<EmailLogEvent>();
    }
}