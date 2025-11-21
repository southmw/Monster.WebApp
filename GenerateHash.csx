#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

var hash = BCrypt.Net.BCrypt.HashPassword("password123");
Console.WriteLine($"Password hash: {hash}");
