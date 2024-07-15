namespace Kafka.Support;

public class KafkaHelper
{
    public static string CreateCustomerTopicName(string email)
    {
        var iLength = email.IndexOf('@');
        var topicName = "customer-" + email.Substring(0, iLength).ToLower() + "-topic";
        return topicName;
    }

    public static string CreateProviderTopicName(string email)
    {
        var iLength = email.IndexOf('@');
        var topicName = "provider-" + email.Substring(0, iLength).ToLower() + "-topic";
        return topicName;
    }

    public static object SubscribedToProviderMessage(string email)
    {
        var message = new SubscriptionActionMessage
        {
            EmailCustomer = email,
            EventDate = DateTime.UtcNow,
        };
        message.SubscriptionActionDescription =
            EnumHelper<SubscriptionAction>.GetEnumDescription(message.SubscriptionAction);
        return message!;
    }
}

public class SubscriptionActionMessage
{
    public string? EmailCustomer { get; set; }
    public DateTime EventDate { get; set; }
    public SubscriptionAction SubscriptionAction { get; set; } = SubscriptionAction.Subscribed;
    public string? SubscriptionActionDescription { get; set; }
}

public enum SubscriptionAction
{
    [Description("Subscribed")] Subscribed,
    [Description("Unsubscribed")] Unsubscribed,
}