// Global using directives

global using System;
global using System.Collections.Generic;
global using System.Security.Claims;
global using System.Threading;
global using System.Threading.Tasks;
global using JetBrains.Annotations;
global using AgendaBuddy.Kafka;
global using AgendaBuddy.Customer.Configurations;
global using AgendaBuddy.Customer.Core.Commands;
global using AgendaBuddy.Customer.Core.Queries;
global using AgendaBuddy.Customer.Domain.Commands;
global using AgendaBuddy.Customer.Domain.Queries;
global using AgendaBuddy.EventAndCommands.Persistence;
global using AgendaBuddy.Library.Dtos;
global using AgendaBuddy.Library.Entities;
global using AgendaBuddy.Library.Services;
global using AgendaBuddy.Library.Tools;
global using MediatR;
global using Microsoft.Extensions.Configuration;
global using MongoDB.Bson;
global using MongoDB.Driver;
global using Moq;
global using Xunit;
