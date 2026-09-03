// Global using directives

global using System;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;
global using AgendaBuddy.Booking.Configuration;
global using AgendaBuddy.Booking.Core.Commands;
global using AgendaBuddy.Booking.Core.Queries;
global using AgendaBuddy.Booking.Domain.Commands;
global using AgendaBuddy.Booking.Domain.Queries;
global using AgendaBuddy.Booking.Requests;
global using AgendaBuddy.EventAndCommands.Persistence;
global using JetBrains.Annotations;
global using AgendaBuddy.Library.Entities;
global using AgendaBuddy.Library.Repositories;
global using AgendaBuddy.Library.Services;
global using MediatR;
global using Microsoft.Extensions.Configuration;
global using MongoDB.Bson;
global using MongoDB.Driver;
global using Moq;
global using Xunit;
