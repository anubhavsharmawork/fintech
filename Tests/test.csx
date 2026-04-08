using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

var services = new ServiceCollection();
var validator = new Moq.Mock<IValidator<string>>();
services.AddSingleton<IValidator<string>>(validator.Object);
var provider = services.BuildServiceProvider();
var resolved = provider.GetService<IValidator<string>>();
Console.WriteLine($'Resolved: {resolved is not null}');
