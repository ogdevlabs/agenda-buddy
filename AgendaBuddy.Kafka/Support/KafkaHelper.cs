using Confluent.Kafka;

namespace AgendaBuddy.Kafka.Support;

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
}
