namespace Library.Events;

public record Subscription(string ConsumerEmail, string ConsumerTopic,  string TopicToSubscribe, DateTime CreatedOn);