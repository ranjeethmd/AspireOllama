using Microsoft.OpenApi;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json.Nodes;


public static class McpOpenApiGenerator
{
    private static OpenApiDocument GenerateFromAssembly(Assembly assembly)
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "MCP Tools API",
                Version = "v1"
            },
            Paths = new OpenApiPaths()
        };

        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        foreach (var toolType in toolTypes)
        {
            var methods = toolType.GetMethods()
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in methods)
            {
                var description =
                    method.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? method.Name;

                IList<IOpenApiParameter> parameters = method.GetParameters()
                    .Select(p => new OpenApiParameter
                    {
                        Name = p.Name,
                        In = ParameterLocation.Query,
                        Required = !p.IsOptional,
                        Description =
                            p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        Schema = MapType(p.ParameterType)
                    }).Cast<IOpenApiParameter>().ToList();

                var path = $"/tools/{toolType.Name}/{method.Name}";

                doc.Paths.Add(path, new OpenApiPathItem
                {
                    Operations = new()
                    {
                        { HttpMethod.Post, new OpenApiOperation
                            {
                                Summary = description,
                                OperationId = method.Name,
                                Parameters = parameters,
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Success"
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }

        return doc;
    }

    private static OpenApiSchema MapType(Type type)
    {
        // Handle nullable types: int?, bool?, etc.
        var underlying = Nullable.GetUnderlyingType(type);

        if (underlying != null)
        {
            var inner = MapType(underlying);

            IList<IOpenApiSchema> oneOf = new List<IOpenApiSchema>
            {
                inner,
                new OpenApiSchema { Type = JsonSchemaType.Null }
            };


            return new OpenApiSchema
            {
                OneOf = oneOf
            };
        }

        // String
        if (type == typeof(string))
            return new OpenApiSchema { Type = JsonSchemaType.String };

        // Integers
        if (type == typeof(int) || type == typeof(uint))
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };

        if (type == typeof(long) || type == typeof(ulong))
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" };

        if (type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };

        // Floats
        if (type == typeof(float))
            return new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" };

        if (type == typeof(double))
            return new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" };

        if (type == typeof(decimal))
            return new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" };

        // Boolean
        if (type == typeof(bool))
            return new OpenApiSchema { Type = JsonSchemaType.Boolean };

        // Date / Time
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };

        if (type == typeof(DateOnly))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };

        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "time" };

        // GUID
        if (type == typeof(Guid))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };

        // Byte array (binary/file content)
        if (type == typeof(byte[]))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };

        // Uri
        if (type == typeof(Uri))
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" };

        if (type.IsEnum)
        {
            IList<JsonNode> enumValues = Enum.GetNames(type)
                .Select(n => (JsonNode)JsonValue.Create(n)!)
                .ToList();

            return new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = enumValues
            };
        }

        // Arrays / Lists
        if (type.IsArray)
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = MapType(type.GetElementType()!)
            };

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();

            // IEnumerable<T>, List<T>, IList<T>, ICollection<T>, HashSet<T>
            if (genericDef == typeof(List<>)
                || genericDef == typeof(IList<>)
                || genericDef == typeof(IEnumerable<>)
                || genericDef == typeof(ICollection<>)
                || genericDef == typeof(IReadOnlyList<>)
                || genericDef == typeof(HashSet<>))
            {
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = MapType(genericArgs[0])
                };
            }

            // Dictionary<TKey, TValue>
            if (genericDef == typeof(Dictionary<,>)
                || genericDef == typeof(IDictionary<,>)
                || genericDef == typeof(IReadOnlyDictionary<,>))
            {
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    AdditionalProperties = MapType(genericArgs[1])
                };
            }
        }

        // Complex object fallback — reflect public properties
        if (type.IsClass || type.IsValueType)
        {
            var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                schema.Properties[prop.Name] = MapType(prop.PropertyType);
            }
            return schema;
        }

        // Fallback
        return new OpenApiSchema { Type = JsonSchemaType.Object };
    }

    extension(WebApplication app)
    {
        public void MapOpenApiMCP(string path = "/openapi/v1.json")
        {
            app.MapGet(path, () =>
            {
                var doc = GenerateFromAssembly(Assembly.GetExecutingAssembly());

                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);

                var jsonWriter = new OpenApiJsonWriter(writer);
                doc.SerializeAsV3(jsonWriter);

                writer.Flush();
                stream.Position = 0;


                return Results.Stream(stream, MediaTypeNames.Application.Json);
            }).WithName("GetMcpOpenApi").WithTags("MCP Tools");
        }
    }
}