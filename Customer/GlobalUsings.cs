global using AgendaBuddy.ServiceDefaults;
global using AgendaBuddy.Library.Configuration;
global using AgendaBuddy.Library.Diagnostics;
// Global using directives

global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Security.Claims;
global using Customer.Configurations;
global using Customer.Events;
global using Customer.Extensions;
global using Customer.Requests;
global using EventAndCommands.Commands.Customer;
global using EventAndCommands.Queries.Customers;
global using Kafka;
global using Kafka.Support;
global using AgendaBuddy.Library.Entities;
global using AgendaBuddy.Library.Extensions;
global using AgendaBuddy.Library.Repositories;
global using AgendaBuddy.Library.Services;
global using AgendaBuddy.Library.Tools;
global using MediatR;
global using Microsoft.AspNetCore.Diagnostics;
global using Microsoft.AspNetCore.Http.HttpResults;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.Net.Http.Headers;
global using MiniValidation;
global using MongoDB.Bson;
global using MongoDB.Driver;
global using EventAndCommands;
global using EventAndCommands.Persistence;
global using AgendaBuddy.Library.Dtos;   // F-016-T15: PageRequest / PagedResponse<T>
