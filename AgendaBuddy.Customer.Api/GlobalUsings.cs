global using AgendaBuddy.ServiceDefaults;
global using AgendaBuddy.Library.Configuration;
global using AgendaBuddy.Library.Diagnostics;
// Global using directives

global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Security.Claims;
global using AgendaBuddy.Customer.Configurations;
global using AgendaBuddy.Customer.Core.Commands;
global using AgendaBuddy.Customer.Core.Queries;
global using AgendaBuddy.Customer.Domain.Commands;
global using AgendaBuddy.Customer.Domain.Queries;
global using AgendaBuddy.Customer.Domain.Responses;
global using AgendaBuddy.Customer.Extensions;
global using AgendaBuddy.Customer.Requests;
global using AgendaBuddy.Library.Dtos;   // PageRequest / PagedResponse<T>
global using AgendaBuddy.Library.Entities;
global using AgendaBuddy.Library.Extensions;
global using AgendaBuddy.Library.Security;
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
global using AgendaBuddy.EventAndCommands;
global using AgendaBuddy.EventAndCommands.Persistence;
global using Carter;
