namespace RaceDay.Api.Swagger;

/// <summary>Small stylesheet that gives Swagger UI the RaceDay colours.</summary>
public static class SwaggerTheme
{
    public const string HeadContent = """
        <style>
          .swagger-ui .topbar { background: #191b1d; border-bottom: 3px solid #ff5a36; }
          .swagger-ui .info .title { color: #191b1d; }
          .swagger-ui .btn.authorize, .swagger-ui .btn.execute { background: #ff5a36; border-color: #ff5a36; color: #fff; }
          .swagger-ui .btn.authorize svg { fill: #fff; }
          .swagger-ui .opblock.opblock-get .opblock-summary-method { background: #3b6255; }
          .swagger-ui .opblock.opblock-post .opblock-summary-method { background: #ff5a36; }
          .swagger-ui .opblock.opblock-put .opblock-summary-method { background: #e8b23d; color: #191b1d; }
        </style>
        """;
}
