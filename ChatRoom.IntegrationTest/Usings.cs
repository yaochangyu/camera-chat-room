global using System.Net;
global using System.Net.Http.Headers;
global using System.Text;
global using System.Text.Json.Nodes;
global using ChatRoom.Data;
global using ChatRoom.Models;
global using FluentAssertions;
global using Json.Path;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.AspNetCore.Http.Connections;
global using Microsoft.AspNetCore.SignalR.Client;
global using Reqnroll;
global using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
