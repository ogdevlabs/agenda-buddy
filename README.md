# Agenda Buddy 
---

Is an application that would allow to service providers to manage their customers. The target audience of the application is to anyone that offers personalized One To One sessions. 

The different professionals that could be finding value in this application, includes the below, however is not limited to other service providers

- Fitness professional
- Tutor / Teacher
- Health and mental professional (Psychologist, Therapist, etc)
- Software professional (Coding lessons, Coding knowledge)

In a nutshell Agenda Buddy will include the following features:
- 
- Provider registration
- Provider define services to offer
- Provider management module (Update profile, Update services, Update customers)
- Customer registration
- Calendar booking management
- Journal and notes
- Messaging Provider - Customer
  
## Launching locally 

Leveraging docker compose to launch locally 

Launch:

```
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d
```
Terminate:
```
docker compose down
```

## Services Ports
- Customer : 6034
- Provider : 6030
- Profession : 6035
- Booking : 6033
- Calendar : 6032
- Services : 6031
- Kafka : 6036
