using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Convey.WebApi.Swagger.Filters;

internal sealed class WebApiDocumentFilter : IDocumentFilter
{
    private const string InBody = "body";
    private const string InQuery = "query";

    private readonly WebApiEndpointDefinitions _definitions;

    private readonly Func<OpenApiPathItem, string, OpenApiOperation> _getOperation = (item, path) =>
    {
        switch (path)
        {
            case "GET":
                item.AddOperation(HttpMethod.Get, new OpenApiOperation());
                return item.Operations[HttpMethod.Get];
            case "POST":
                item.AddOperation(HttpMethod.Post, new OpenApiOperation());
                return item.Operations[HttpMethod.Post];
            case "PUT":
                item.AddOperation(HttpMethod.Put, new OpenApiOperation());
                return item.Operations[HttpMethod.Put];
            case "DELETE":
                item.AddOperation(HttpMethod.Delete, new OpenApiOperation());
                return item.Operations[HttpMethod.Delete];
            case "HEAD":
                item.AddOperation(HttpMethod.Head, new OpenApiOperation());
                return item.Operations[HttpMethod.Head];
            case "PATCH":
                item.AddOperation(HttpMethod.Patch, new OpenApiOperation());
                return item.Operations[HttpMethod.Patch];
            case "OPTIONS":
                item.AddOperation(HttpMethod.Options, new OpenApiOperation());
                return item.Operations[HttpMethod.Options];
        }

        return null;
    };

    public WebApiDocumentFilter(WebApiEndpointDefinitions definitions)
        => _definitions = definitions;

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        foreach (var pathDefinition in _definitions.GroupBy(d => d.Path))
        {
            var pathItem = new OpenApiPathItem();

            foreach (var methodDefinition in pathDefinition)
            {
                var operation = _getOperation(pathItem, methodDefinition.Method);
                operation.Responses = [];
                operation.Parameters = [];

                foreach (var parameter in methodDefinition.Parameters)
                {
                    if (parameter.In is InBody)
                    {
                        operation.RequestBody = new OpenApiRequestBody
                        {
                            Content = new Dictionary<string, OpenApiMediaType>()
                            {
                                {
                                    "application/json", new OpenApiMediaType()
                                    {
                                        Schema = new OpenApiSchema
                                        {
                                            Title = parameter.Name,
                                            Description = parameter.Type.Name,
                                            Example = JsonNode.Parse(
                                                JsonSerializer.Serialize(parameter.Example,
                                                    jsonSerializerOptions))
                                        }
                                    }
                                }
                            }
                        };
                    }
                    else if (parameter.In is InQuery)
                    {
                        if (parameter.Type.GetInterface("IQuery") is not null)
                        {
                            operation.RequestBody = new OpenApiRequestBody()
                            {
                                Content = new Dictionary<string, OpenApiMediaType>()
                                {
                                    {
                                        "application/json", new OpenApiMediaType()
                                        {
                                            Schema = new OpenApiSchema
                                            {
                                                Title = parameter.Name,
                                                Description = parameter.Type.Name,
                                                Example = JsonNode.Parse(
                                                    JsonSerializer.Serialize(parameter.Example,
                                                        jsonSerializerOptions))
                                            }
                                        }
                                    }
                                }
                            };
                        }
                        else
                        {
                            operation.Parameters.Add(new OpenApiParameter
                            {
                                Name = parameter.Name,
                                Schema = new OpenApiSchema
                                {
                                    Title = parameter.Name,
                                    Description = parameter.Type.Name,
                                    Example = JsonNode.Parse(
                                        JsonSerializer.Serialize(parameter.Example,
                                            jsonSerializerOptions))
                                }
                            });
                        }
                    }
                }

                foreach (var response in methodDefinition.Responses)
                {
                    operation.Responses.Add(response.StatusCode.ToString(), new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            {
                                "application/json", new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchema
                                    {
                                        Title = response.Type.Name,
                                        Example = JsonNode.Parse(
                                            JsonSerializer.Serialize(response.Example,
                                                jsonSerializerOptions))
                                    }
                                }
                            }
                        }
                    });
                }
            }

            swaggerDoc.Paths.Add($"/{pathDefinition.Key}", pathItem);
        }
    }
}