global using AgendaBuddy.ServiceDefaults;
global using AgendaBuddy.Library.Configuration;
global using AgendaBuddy.Library.Diagnostics;
// Global using directives

global using System.Diagnostics;
global using System.Net;
global using System.Security.Claims;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using EventAndCommands.Commands.Provider;
global using EventAndCommands.Queries.Provider;
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
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Net.Http.Headers;
global using MiniValidation;
global using MongoDB.Driver;
global using Provider.Configurations;
global using Provider.Events;
global using Provider.Extensions;
global using Provider.Requests;
global using ProviderService = AgendaBuddy.Library.Services.ProviderService;
global using EventAndCommands;
global using EventAndCommands.Persistence;
global using AgendaBuddy.Library.Dtos;   // F-016-T15: PageRequest / PagedResponse<T>
