using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Commands;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.LoanOriginationService.Tests.Workers;

public class InitialValidationWorkerTests
{
    private readonly Mock<IZeebeClient> _mockZeebeClient;
    private readonly Mock<IJobClient> _mockJobClient;
    private readonly Mock<IJob> _mockJob;
    private readonly Mock<ILogger<InitialValidationWorker>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public InitialValidationWorkerTests()
    {
        _mockZeebeClient = new Mock<IZeebeClient>();
        _mockJobClient = new Mock<IJobClient>();
        _mockJob = new Mock<IJob>();
        _mockLogger = new Mock<ILogger<InitialValidationWorker>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup service scope
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetRequiredService<ILogger<InitialValidationWorker>>())
            .Returns(_mockLogger.Object);
    }

    [Fact]
    public async Task HandleInitialValidationJob_ValidInput_CompletesJob()
    {
        // Arrange
        var worker = new InitialValidationWorker(_mockZeebeClient.Object, _mockLogger.Object, _mockServiceScopeFactory.Object);
        
        var validVariables = new LoanApplicationVariables
        {
            LoanAmount = 5000.00m,
            ProductType = "PAYROLL",
            ApplicantNrc = "123456/78/9",
            BranchId = "BR001"
        };

        var jobVariables = JsonSerializer.Serialize(validVariables);
        
        _mockJob.Setup(x => x.Key).Returns(12345L);
        _mockJob.Setup(x => x.Variables).Returns(jobVariables);

        var mockCompleteCommand = new Mock<ICompleteJobCommandStep1>();
        var mockCompleteCommandStep2 = new Mock<ICompleteJobCommandStep2>();
        
        _mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(mockCompleteCommand.Object);
        mockCompleteCommand.Setup(x => x.Variables(It.IsAny<Dictionary<string, object>>()))
            .Returns(mockCompleteCommandStep2.Object);
        mockCompleteCommandStep2.Setup(x => x.Send())
            .Returns(Task.FromResult(Mock.Of<ICompleteJobResponse>()));

        // Act
        await worker.TestHandleInitialValidationJob(_mockJobClient.Object, _mockJob.Object);

        // Assert
        _mockJobClient.Verify(x => x.NewCompleteJobCommand(12345L), Times.Once);
        mockCompleteCommand.Verify(x => x.Variables(It.Is<Dictionary<string, object>>(dict => 
            dict.ContainsKey("isValid") && (bool)dict["isValid"] == true)), Times.Once);
        mockCompleteCommandStep2.Verify(x => x.Send(), Times.Once);
    }

    [Theory]
    [InlineData(0, "PAYROLL", "123456/78/9", "BR001", "Loan amount must be greater than zero")]
    [InlineData(-100, "PAYROLL", "123456/78/9", "BR001", "Loan amount must be greater than zero")]
    [InlineData(5000, "", "123456/78/9", "BR001", "Product type is required")]
    [InlineData(5000, "INVALID", "123456/78/9", "BR001", "Product type must be either 'PAYROLL' or 'BUSINESS'")]
    [InlineData(5000, "PAYROLL", "", "BR001", "Applicant NRC is required")]
    [InlineData(5000, "PAYROLL", "123456/78/9", "", "Branch ID is required")]
    public async Task HandleInitialValidationJob_InvalidInput_ThrowsError(
        decimal loanAmount, 
        string productType, 
        string applicantNrc, 
        string branchId, 
        string expectedErrorPrefix)
    {
        // Arrange
        var worker = new InitialValidationWorker(_mockZeebeClient.Object, _mockLogger.Object, _mockServiceScopeFactory.Object);
        
        var invalidVariables = new LoanApplicationVariables
        {
            LoanAmount = loanAmount,
            ProductType = productType,
            ApplicantNrc = applicantNrc,
            BranchId = branchId
        };

        var jobVariables = JsonSerializer.Serialize(invalidVariables);
        
        _mockJob.Setup(x => x.Key).Returns(12345L);
        _mockJob.Setup(x => x.Variables).Returns(jobVariables);

        var mockErrorCommand = new Mock<IThrowErrorCommandStep1>();
        var mockErrorCommandStep2 = new Mock<IThrowErrorCommandStep2>();
        var mockErrorCommandStep3 = new Mock<IThrowErrorCommandStep3>();
        
        _mockJobClient.Setup(x => x.NewThrowErrorCommand(It.IsAny<long>()))
            .Returns(mockErrorCommand.Object);
        mockErrorCommand.Setup(x => x.ErrorCode("validation-error"))
            .Returns(mockErrorCommandStep2.Object);
        mockErrorCommandStep2.Setup(x => x.ErrorMessage(It.IsAny<string>()))
            .Returns(mockErrorCommandStep3.Object);
        mockErrorCommandStep3.Setup(x => x.Send())
            .Returns(Task.FromResult(Mock.Of<IThrowErrorResponse>()));

        // Act
        await worker.TestHandleInitialValidationJob(_mockJobClient.Object, _mockJob.Object);

        // Assert
        _mockJobClient.Verify(x => x.NewThrowErrorCommand(12345L), Times.Once);
        mockErrorCommand.Verify(x => x.ErrorCode("validation-error"), Times.Once);
        mockErrorCommandStep2.Verify(x => x.ErrorMessage(It.Is<string>(msg => msg.Contains(expectedErrorPrefix))), Times.Once);
        mockErrorCommandStep3.Verify(x => x.Send(), Times.Once);
    }

    [Fact]
    public async Task HandleInitialValidationJob_ValidBusinessLoan_CompletesJob()
    {
        // Arrange
        var worker = new InitialValidationWorker(_mockZeebeClient.Object, _mockLogger.Object, _mockServiceScopeFactory.Object);
        
        var validVariables = new LoanApplicationVariables
        {
            LoanAmount = 25000.00m,
            ProductType = "BUSINESS",
            ApplicantNrc = "987654/32/1",
            BranchId = "BR002"
        };

        var jobVariables = JsonSerializer.Serialize(validVariables);
        
        _mockJob.Setup(x => x.Key).Returns(67890L);
        _mockJob.Setup(x => x.Variables).Returns(jobVariables);

        var mockCompleteCommand = new Mock<ICompleteJobCommandStep1>();
        var mockCompleteCommandStep2 = new Mock<ICompleteJobCommandStep2>();
        
        _mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(mockCompleteCommand.Object);
        mockCompleteCommand.Setup(x => x.Variables(It.IsAny<Dictionary<string, object>>()))
            .Returns(mockCompleteCommandStep2.Object);
        mockCompleteCommandStep2.Setup(x => x.Send())
            .Returns(Task.FromResult(Mock.Of<ICompleteJobResponse>()));

        // Act
        await worker.TestHandleInitialValidationJob(_mockJobClient.Object, _mockJob.Object);

        // Assert
        _mockJobClient.Verify(x => x.NewCompleteJobCommand(67890L), Times.Once);
        mockCompleteCommand.Verify(x => x.Variables(It.Is<Dictionary<string, object>>(dict => 
            dict.ContainsKey("isValid") && (bool)dict["isValid"] == true)), Times.Once);
        mockCompleteCommandStep2.Verify(x => x.Send(), Times.Once);
    }

    [Fact]
    public async Task HandleInitialValidationJob_InvalidJsonVariables_ThrowsDeserializationError()
    {
        // Arrange
        var worker = new InitialValidationWorker(_mockZeebeClient.Object, _mockLogger.Object, _mockServiceScopeFactory.Object);
        
        _mockJob.Setup(x => x.Key).Returns(12345L);
        _mockJob.Setup(x => x.Variables).Returns("invalid json");

        var mockErrorCommand = new Mock<IThrowErrorCommandStep1>();
        var mockErrorCommandStep2 = new Mock<IThrowErrorCommandStep2>();
        var mockErrorCommandStep3 = new Mock<IThrowErrorCommandStep3>();
        
        _mockJobClient.Setup(x => x.NewThrowErrorCommand(It.IsAny<long>()))
            .Returns(mockErrorCommand.Object);
        mockErrorCommand.Setup(x => x.ErrorCode("deserialization-error"))
            .Returns(mockErrorCommandStep2.Object);
        mockErrorCommandStep2.Setup(x => x.ErrorMessage("Failed to deserialize job variables"))
            .Returns(mockErrorCommandStep3.Object);
        mockErrorCommandStep3.Setup(x => x.Send())
            .Returns(Task.FromResult(Mock.Of<IThrowErrorResponse>()));

        // Act
        await worker.TestHandleInitialValidationJob(_mockJobClient.Object, _mockJob.Object);

        // Assert
        _mockJobClient.Verify(x => x.NewThrowErrorCommand(12345L), Times.Once);
        mockErrorCommand.Verify(x => x.ErrorCode("deserialization-error"), Times.Once);
        mockErrorCommandStep2.Verify(x => x.ErrorMessage("Failed to deserialize job variables"), Times.Once);
        mockErrorCommandStep3.Verify(x => x.Send(), Times.Once);
    }
}

// Extension class to make the private method testable
public static class InitialValidationWorkerExtensions
{
    public static async Task TestHandleInitialValidationJob(this InitialValidationWorker worker, IJobClient jobClient, IJob job)
    {
        var method = typeof(InitialValidationWorker).GetMethod("HandleInitialValidationJob", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            await (Task)method.Invoke(worker, new object[] { jobClient, job })!;
        }
    }
}