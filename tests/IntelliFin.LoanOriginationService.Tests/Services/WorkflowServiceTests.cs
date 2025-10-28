using IntelliFin.LoanOriginationService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using Zeebe.Client;
using Zeebe.Client.Api.Commands;
using Zeebe.Client.Api.Responses;

namespace IntelliFin.LoanOriginationService.Tests.Services;

public class WorkflowServiceTests
{
    private readonly Mock<IZeebeClient> _mockZeebeClient;
    private readonly Mock<ILogger<WorkflowService>> _mockLogger;
    private readonly WorkflowService _service;

    public WorkflowServiceTests()
    {
        _mockZeebeClient = new Mock<IZeebeClient>();
        _mockLogger = new Mock<ILogger<WorkflowService>>();

        _service = new WorkflowService(_mockLogger.Object, _mockZeebeClient.Object);
    }

    [Fact]
    public async Task StartLoanOriginationWorkflowAsync_ShouldCreateProcessInstanceWithCorrectVariables()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var loanAmount = 75000m;
        var riskGrade = "B";
        var productCode = "GEPL-001";
        var termMonths = 24;
        var createdBy = "user123";
        var loanNumber = "LUS-2025-00123";

        var mockCreateCommand = new Mock<ICreateProcessInstanceCommandStep1>();
        var mockCreateCommandStep2 = new Mock<ICreateProcessInstanceCommandStep2>();
        var mockCreateCommandStep3 = new Mock<ICreateProcessInstanceCommandStep3>();
        var mockProcessInstance = new Mock<IProcessInstanceResult>();

        // Setup process instance response
        mockProcessInstance.Setup(p => p.ProcessInstanceKey).Returns(987654321L);
        mockProcessInstance.Setup(p => p.ProcessDefinitionKey).Returns(12345L);

        // Setup Zeebe client command chain
        _mockZeebeClient
            .Setup(c => c.NewCreateProcessInstanceCommand())
            .Returns(mockCreateCommand.Object);

        mockCreateCommand
            .Setup(c => c.BpmnProcessId("loanOriginationProcess"))
            .Returns(mockCreateCommandStep2.Object);

        mockCreateCommandStep2
            .Setup(c => c.LatestVersion())
            .Returns(mockCreateCommandStep3.Object);

        mockCreateCommandStep3
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockCreateCommandStep3.Object);

        mockCreateCommandStep3
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockProcessInstance.Object);

        // Act
        var workflowInstanceKey = await _service.StartLoanOriginationWorkflowAsync(
            applicationId,
            clientId,
            loanAmount,
            riskGrade,
            productCode,
            termMonths,
            createdBy,
            loanNumber,
            CancellationToken.None);

        // Assert
        Assert.Equal("987654321", workflowInstanceKey);

        // Verify Zeebe client was called correctly
        _mockZeebeClient.Verify(c => c.NewCreateProcessInstanceCommand(), Times.Once);
        mockCreateCommand.Verify(c => c.BpmnProcessId("loanOriginationProcess"), Times.Once);
        mockCreateCommandStep2.Verify(c => c.LatestVersion(), Times.Once);
        
        // Verify variables were serialized and passed
        mockCreateCommandStep3.Verify(
            c => c.Variables(It.Is<string>(json => 
                json.Contains(applicationId.ToString()) &&
                json.Contains(clientId.ToString()) &&
                json.Contains("75000") &&
                json.Contains("B") &&
                json.Contains("GEPL-001") &&
                json.Contains("24") &&
                json.Contains("user123") &&
                json.Contains("LUS-2025-00123")
            )),
            Times.Once);

        mockCreateCommandStep3.Verify(c => c.Send(It.IsAny<CancellationToken>()), Times.Once);

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting loan origination workflow")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully started loan origination workflow")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartLoanOriginationWorkflowAsync_ShouldSerializeVariablesCorrectly()
    {
        // Arrange
        var applicationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var clientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var loanAmount = 50000.50m;
        var riskGrade = "A";
        var productCode = "SEPL-002";
        var termMonths = 36;
        var createdBy = "admin";
        var loanNumber = "LUS-2025-99999";

        var mockCreateCommand = new Mock<ICreateProcessInstanceCommandStep1>();
        var mockCreateCommandStep2 = new Mock<ICreateProcessInstanceCommandStep2>();
        var mockCreateCommandStep3 = new Mock<ICreateProcessInstanceCommandStep3>();
        var mockProcessInstance = new Mock<IProcessInstanceResult>();

        mockProcessInstance.Setup(p => p.ProcessInstanceKey).Returns(111111L);
        mockProcessInstance.Setup(p => p.ProcessDefinitionKey).Returns(22222L);

        _mockZeebeClient.Setup(c => c.NewCreateProcessInstanceCommand()).Returns(mockCreateCommand.Object);
        mockCreateCommand.Setup(c => c.BpmnProcessId("loanOriginationProcess")).Returns(mockCreateCommandStep2.Object);
        mockCreateCommandStep2.Setup(c => c.LatestVersion()).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Variables(It.IsAny<string>())).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Send(It.IsAny<CancellationToken>())).ReturnsAsync(mockProcessInstance.Object);

        string? capturedVariables = null;
        mockCreateCommandStep3
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Callback<string>(json => capturedVariables = json)
            .Returns(mockCreateCommandStep3.Object);

        // Act
        await _service.StartLoanOriginationWorkflowAsync(
            applicationId, clientId, loanAmount, riskGrade, productCode, termMonths, createdBy, loanNumber);

        // Assert
        Assert.NotNull(capturedVariables);

        // Deserialize and verify JSON structure
        var variables = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(capturedVariables!);
        Assert.NotNull(variables);
        Assert.Equal("11111111-1111-1111-1111-111111111111", variables!["applicationId"].GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", variables["clientId"].GetString());
        Assert.Equal(50000.50m, variables["loanAmount"].GetDecimal());
        Assert.Equal("A", variables["riskGrade"].GetString());
        Assert.Equal("SEPL-002", variables["productCode"].GetString());
        Assert.Equal(36, variables["termMonths"].GetInt32());
        Assert.Equal("admin", variables["createdBy"].GetString());
        Assert.Equal("LUS-2025-99999", variables["loanNumber"].GetString());
    }

    [Fact]
    public async Task StartLoanOriginationWorkflowAsync_WhenZeebeClientThrowsException_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var mockCreateCommand = new Mock<ICreateProcessInstanceCommandStep1>();
        var mockCreateCommandStep2 = new Mock<ICreateProcessInstanceCommandStep2>();
        var mockCreateCommandStep3 = new Mock<ICreateProcessInstanceCommandStep3>();

        _mockZeebeClient.Setup(c => c.NewCreateProcessInstanceCommand()).Returns(mockCreateCommand.Object);
        mockCreateCommand.Setup(c => c.BpmnProcessId("loanOriginationProcess")).Returns(mockCreateCommandStep2.Object);
        mockCreateCommandStep2.Setup(c => c.LatestVersion()).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Variables(It.IsAny<string>())).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Zeebe connection timeout"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _service.StartLoanOriginationWorkflowAsync(
                applicationId, 
                clientId, 
                100000m, 
                "C", 
                "GEPL-001", 
                12, 
                "user", 
                "LUS-2025-00001"));

        Assert.Contains("Zeebe connection timeout", exception.Message);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error starting loan origination workflow")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartLoanOriginationWorkflowAsync_ShouldUseLatestVersionOfWorkflow()
    {
        // Arrange
        var mockCreateCommand = new Mock<ICreateProcessInstanceCommandStep1>();
        var mockCreateCommandStep2 = new Mock<ICreateProcessInstanceCommandStep2>();
        var mockCreateCommandStep3 = new Mock<ICreateProcessInstanceCommandStep3>();
        var mockProcessInstance = new Mock<IProcessInstanceResult>();

        mockProcessInstance.Setup(p => p.ProcessInstanceKey).Returns(555555L);
        mockProcessInstance.Setup(p => p.ProcessDefinitionKey).Returns(66666L);

        _mockZeebeClient.Setup(c => c.NewCreateProcessInstanceCommand()).Returns(mockCreateCommand.Object);
        mockCreateCommand.Setup(c => c.BpmnProcessId("loanOriginationProcess")).Returns(mockCreateCommandStep2.Object);
        mockCreateCommandStep2.Setup(c => c.LatestVersion()).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Variables(It.IsAny<string>())).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Send(It.IsAny<CancellationToken>())).ReturnsAsync(mockProcessInstance.Object);

        // Act
        await _service.StartLoanOriginationWorkflowAsync(
            Guid.NewGuid(), Guid.NewGuid(), 50000m, "A", "GEPL-001", 24, "user", "LUS-2025-00001");

        // Assert
        mockCreateCommandStep2.Verify(c => c.LatestVersion(), Times.Once);
    }

    [Theory]
    [InlineData("A", 25000)]
    [InlineData("B", 50000)]
    [InlineData("C", 100000)]
    [InlineData("D", 300000)]
    [InlineData("F", 500000)]
    public async Task StartLoanOriginationWorkflowAsync_ShouldHandleDifferentRiskGradesAndAmounts(string riskGrade, decimal loanAmount)
    {
        // Arrange
        var mockCreateCommand = new Mock<ICreateProcessInstanceCommandStep1>();
        var mockCreateCommandStep2 = new Mock<ICreateProcessInstanceCommandStep2>();
        var mockCreateCommandStep3 = new Mock<ICreateProcessInstanceCommandStep3>();
        var mockProcessInstance = new Mock<IProcessInstanceResult>();

        mockProcessInstance.Setup(p => p.ProcessInstanceKey).Returns(123456L);
        mockProcessInstance.Setup(p => p.ProcessDefinitionKey).Returns(789L);

        _mockZeebeClient.Setup(c => c.NewCreateProcessInstanceCommand()).Returns(mockCreateCommand.Object);
        mockCreateCommand.Setup(c => c.BpmnProcessId("loanOriginationProcess")).Returns(mockCreateCommandStep2.Object);
        mockCreateCommandStep2.Setup(c => c.LatestVersion()).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Variables(It.IsAny<string>())).Returns(mockCreateCommandStep3.Object);
        mockCreateCommandStep3.Setup(c => c.Send(It.IsAny<CancellationToken>())).ReturnsAsync(mockProcessInstance.Object);

        // Act
        var result = await _service.StartLoanOriginationWorkflowAsync(
            Guid.NewGuid(), Guid.NewGuid(), loanAmount, riskGrade, "GEPL-001", 24, "user", "LUS-2025-00001");

        // Assert
        Assert.Equal("123456", result);
        
        // Verify workflow was started with correct variables
        mockCreateCommandStep3.Verify(
            c => c.Variables(It.Is<string>(json => 
                json.Contains(riskGrade) && json.Contains(loanAmount.ToString())
            )),
            Times.Once);
    }
}
