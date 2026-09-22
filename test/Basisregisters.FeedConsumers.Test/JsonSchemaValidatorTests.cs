namespace Basisregisters.FeedConsumers.Test;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class JsonSchemaValidatorTests
{
    private const string SchemaUri = "https://test/schemas/postinfo.json";

    // Uses if/then conditionals, which our previous validator (NJsonSchema) silently ignored.
    private const string ConditionalSchema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "required": ["attributen"],
          "properties": {
            "attributen": {
              "type": "array",
              "items": { "$ref": "#/$defs/attribuutChange" }
            }
          },
          "$defs": {
            "attribuutChange": {
              "type": "object",
              "required": ["naam", "nieuweWaarde"],
              "allOf": [
                {
                  "if": { "properties": { "naam": { "const": "status" } }, "required": ["naam"] },
                  "then": { "properties": { "nieuweWaarde": { "enum": ["gerealiseerd", "gehistoreerd"] } } }
                }
              ]
            }
          }
        }
        """;

    public static TheoryData<string> TestDataFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles("TestData", "*.json").Select(Path.GetFileName).Order())
            data.Add(file!);
        return data;
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(TestDataFiles))]
    public async Task TestDataEvents_ShouldMatchPublishedSchema(string fileName)
    {
        using var httpClient = new HttpClient();
        var validator = new JsonSchemaValidator(httpClient, NullLogger.Instance);

        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(Path.Combine("TestData", fileName));

        var failures = new List<string>();
        foreach (var cloudEvent in events)
        {
            try
            {
                await validator.ValidateAsync(cloudEvent, TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                failures.Add(ex.Message);
            }
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task ConditionalRuleViolation_ShouldThrow()
    {
        var validator = CreateValidatorWithSchema(ConditionalSchema);
        var cloudEvent = await CreateEventAsync("""{ "attributen": [ { "naam": "status", "nieuweWaarde": "foo" } ] }""");

        var act = () => validator.ValidateAsync(cloudEvent, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to validate JSON schema for event 1*");
    }

    [Fact]
    public async Task ValidData_ShouldNotThrow()
    {
        var validator = CreateValidatorWithSchema(ConditionalSchema);
        var cloudEvent = await CreateEventAsync("""{ "attributen": [ { "naam": "status", "nieuweWaarde": "gerealiseerd" } ] }""");

        var act = () => validator.ValidateAsync(cloudEvent, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    private static JsonSchemaValidator CreateValidatorWithSchema(string schema)
        => new(new HttpClient(new StubSchemaHandler(schema)), NullLogger.Instance);

    private static async Task<CloudNative.CloudEvents.CloudEvent> CreateEventAsync(string data)
    {
        var events = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            $$"""
            [
              {
                "specversion": "1.0",
                "id": "1",
                "time": "2020-02-10T12:42:50+01:00",
                "type": "basisregisters.postalinformation.update.v1",
                "source": "https://test/feed",
                "subject": "https://data.vlaanderen.be/id/postinfo/9050",
                "datacontenttype": "application/json",
                "dataschema": "{{SchemaUri}}",
                "data": {{data}}
              }
            ]
            """);
        return events.Single();
    }

    private sealed class StubSchemaHandler(string schema) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(schema, Encoding.UTF8, "application/json")
            });
    }
}
