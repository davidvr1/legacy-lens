using System.Text.Json.Serialization;
using LegacyLens.Core.Rules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IRule, SqlConcatenationRule>();
builder.Services.AddSingleton<IRule, XssOutputRule>();
builder.Services.AddSingleton<IRule, HardCodedSecretRule>();
builder.Services.AddSingleton<IRule, OnErrorResumeNextRule>();
builder.Services.AddSingleton<IRule, ComObjectRule>();
builder.Services.AddSingleton<IRule, SessionUsageRule>();
builder.Services.AddSingleton<IRule, IncludeRule>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
