// Global using directives

global using System.Diagnostics;
global using System.Net;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using EventAndCommands.Commands.Provider;
global using EventAndCommands.Queries.Provider;
global using Kafka;
global using Kafka.Support;
global using Library.Entities;
global using Library.Repositories;
global using Library.Services;
global using Library.Tools;
global using MediatR;
global using Microsoft.AspNetCore.Diagnostics;
global using Microsoft.AspNetCore.Http.HttpResults;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Net.Http.Headers;
global using MiniValidation;
global using MongoDB.Driver;
global using Provider.Configurations;
global using Provider.Events;
global using Provider.Extensions;
global using Provider.Requests;
global using ProviderService = Library.Services.ProviderService;