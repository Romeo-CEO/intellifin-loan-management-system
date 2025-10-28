using IntelliFin.LoanOriginationService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Zeebe.Client;
using Zeebe.Client.Api.Commands;
using Zeebe.Client.Api.Responses;

namespace IntelliFin.LoanOriginationService.Tests.Services;

public class BpmnDeploymentServiceTests
{
    private readonly Mock<IZeebeClient> _mockZeebeClient;
    private readonly Mock<ILogger<BpmnDeploymentService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly BpmnDeploymentService _service;

    public BpmnDeploymentServiceTests()
    {
        _mockZeebeClient = new Mock<IZeebeClient>();
        _mockLogger = new Mock<ILogger<BpmnDeploymentService>>();
        _mockConfiguration = new Mock<IConfiguration>();

        _service = new BpmnDeploymentService(
            _mockZeebeClient.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task StartAsync_WhenBpmnFileExists_ShouldDeploySuccessfully()
    {
        // Arrange
        var mockDeployCommand = new Mock<IDeployResourceCommandStep1>();
        var mockDeployCommandStep2 = new Mock<IDeployResourceCommandStep2>();
        var mockDeployResponse = new Mock<IDeployResponse>();
        var mockWorkflow = new Mock<IWorkflowMetadata>();

        // Setup workflow metadata
        mockWorkflow.Setup(w => w.WorkflowKey).Returns(123456L);
        mockWorkflow.Setup(w => w.BpmnProcessId).Returns("loanOriginationProcess");
        mockWorkflow.Setup(w => w.Version).Returns(1);
        mockWorkflow.Setup(w => w.ResourceName).Returns("loan-origination-process.bpmn");

        // Setup deploy response
        mockDeployResponse.Setup(r => r.Workflows).Returns(new List<IWorkflowMetadata> { mockWorkflow.Object });

        // Setup Zeebe client command chain
        _mockZeebeClient
            .Setup(c => c.NewDeployCommand())
            .Returns(mockDeployCommand.Object);

        mockDeployCommand
            .Setup(c => c.AddResourceStringUtf8(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockDeployCommandStep2.Object);

        mockDeployCommandStep2
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeployResponse.Object);

        // Create a temporary BPMN file for testing
        var testBpmnContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""loanOriginationProcess"" name=""Test Process"" isExecutable=""true"">
  </bpmn:process>
</bpmn:definitions>";
        var tempBpmnPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        Directory.CreateDirectory(tempBpmnPath);
        var tempBpmnFile = Path.Combine(tempBpmnPath, "loan-origination-process.bpmn");
        await File.WriteAllTextAsync(tempBpmnFile, testBpmnContent);

        try
        {
            // Act
            await _service.StartAsync(CancellationToken.None);

            // Assert
            _mockZeebeClient.Verify(c => c.NewDeployCommand(), Times.Once);
            mockDeployCommand.Verify(
                c => c.AddResourceStringUtf8(It.IsAny<string>(), "loan-origination-process.bpmn"),
                Times.Once);
            mockDeployCommandStep2.Verify(c => c.Send(It.IsAny<CancellationToken>()), Times.Once);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully deployed BPMN workflow")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempBpmnFile))
                File.Delete(tempBpmnFile);
        }
    }

    [Fact]
    public async Task StartAsync_WhenBpmnFileDoesNotExist_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var tempBpmnPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        var tempBpmnFile = Path.Combine(tempBpmnPath, "loan-origination-process.bpmn");
        
        // Ensure file doesn't exist
        if (File.Exists(tempBpmnFile))
            File.Delete(tempBpmnFile);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.StartAsync(CancellationToken.None));

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deploy BPMN workflow")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenZeebeDeploymentFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockDeployCommand = new Mock<IDeployResourceCommandStep1>();
        var mockDeployCommandStep2 = new Mock<IDeployResourceCommandStep2>();
        var mockDeployResponse = new Mock<IDeployResponse>();

        // Setup deploy response with no workflows (deployment failure)
        mockDeployResponse.Setup(r => r.Workflows).Returns(new List<IWorkflowMetadata>());

        // Setup Zeebe client command chain
        _mockZeebeClient
            .Setup(c => c.NewDeployCommand())
            .Returns(mockDeployCommand.Object);

        mockDeployCommand
            .Setup(c => c.AddResourceStringUtf8(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockDeployCommandStep2.Object);

        mockDeployCommandStep2
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeployResponse.Object);

        // Create a temporary BPMN file for testing
        var testBpmnContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""loanOriginationProcess"" name=""Test Process"" isExecutable=""true"">
  </bpmn:process>
</bpmn:definitions>";
        var tempBpmnPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        Directory.CreateDirectory(tempBpmnPath);
        var tempBpmnFile = Path.Combine(tempBpmnPath, "loan-origination-process.bpmn");
        await File.WriteAllTextAsync(tempBpmnFile, testBpmnContent);

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.StartAsync(CancellationToken.None));

            Assert.Contains("BPMN deployment failed", exception.Message);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deploy BPMN workflow")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempBpmnFile))
                File.Delete(tempBpmnFile);
        }
    }

    [Fact]
    public async Task StartAsync_WhenZeebeClientThrowsException_ShouldPropagateException()
    {
        // Arrange
        var mockDeployCommand = new Mock<IDeployResourceCommandStep1>();
        var mockDeployCommandStep2 = new Mock<IDeployResourceCommandStep2>();

        // Setup Zeebe client to throw exception
        _mockZeebeClient
            .Setup(c => c.NewDeployCommand())
            .Returns(mockDeployCommand.Object);

        mockDeployCommand
            .Setup(c => c.AddResourceStringUtf8(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockDeployCommandStep2.Object);

        mockDeployCommandStep2
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Zeebe connection failed"));

        // Create a temporary BPMN file for testing
        var testBpmnContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""loanOriginationProcess"" name=""Test Process"" isExecutable=""true"">
  </bpmn:process>
</bpmn:definitions>";
        var tempBpmnPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        Directory.CreateDirectory(tempBpmnPath);
        var tempBpmnFile = Path.Combine(tempBpmnPath, "loan-origination-process.bpmn");
        await File.WriteAllTextAsync(tempBpmnFile, testBpmnContent);

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _service.StartAsync(CancellationToken.None));

            Assert.Contains("Zeebe connection failed", exception.Message);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deploy BPMN workflow")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempBpmnFile))
                File.Delete(tempBpmnFile);
        }
    }

    [Fact]
    public async Task StopAsync_ShouldCompleteSuccessfully()
    {
        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert - Should complete without exceptions
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BpmnDeploymentService stopping")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
