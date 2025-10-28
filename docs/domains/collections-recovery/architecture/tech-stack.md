# Tech Stack

### Existing Technology Stack

| Category  | Current Technology | Version | Usage in Enhancement | Notes                                        |
| :-------- | :----------------- | :------ | :------------------- | :------------------------------------------- |
| Runtime   | .NET               | 9.0     | CollectionsService   | Latest .NET framework                        |
| Framework | ASP.NET Core       | 9.0     | CollectionsService   | Web application framework                    |
| Messaging | RabbitMQ           | 3.x     | CollectionsService   | Via MassTransit, used for inter-service communication |
| Config    | HashiCorp Vault    | N/A     | CollectionsService   | For centralized secret and config management |
| Observability | OpenTelemetry  | N/A     | CollectionsService   | Via `IntelliFin.Shared.Observability`        |
| Workflow  | Camunda (Zeebe)    | Self-Hosted | CollectionsService | For process orchestration and external tasks |
| Database  | SQL Server         | N/A     | CollectionsService   | Standard for IntelliFin services             |
