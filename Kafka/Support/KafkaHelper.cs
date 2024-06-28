using Confluent.Kafka;

namespace Kafka.Support;

public class KafkaHelper
{
    public static string CreateCustomerTopicName(string email)
    {
        var iLength = email.IndexOf('@');
        var topicName = "customer_"+email.Substring(0, iLength).ToLower() + "-topic";
        return topicName;
    }
    
    public static string CreateProviderTopicName(string email)
    {
        var iLength = email.IndexOf('@');
        var topicName = "provider_"+email.Substring(0, iLength).ToLower() + "-topic";
        return topicName;
    }
}