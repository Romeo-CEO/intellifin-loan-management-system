using System;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Http;

namespace IntelliFin.Tests.Integration.AdminService;

public sealed class TestCamundaHandlerFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly TestCamundaHandler _handler;

    public TestCamundaHandlerFilter(TestCamundaHandler handler)
    {
        _handler = handler;
    }

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);

            if (builder.Name == typeof(ICamundaWorkflowService).FullName || builder.Name == typeof(CamundaWorkflowService).FullName)
            {
                builder.PrimaryHandler = _handler;
            }
        };
    }
}
