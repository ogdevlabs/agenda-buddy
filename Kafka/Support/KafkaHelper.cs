using Confluent.Kafka;

namespace Kafka.Support;

public class KafkaHelper
{
    public static string CreateTopicName(string email)
    {
        var iLength = email.IndexOf('@');
        var topicName = email.Substring(0, iLength).ToLower() + "-topic";
        return topicName;
    }
}