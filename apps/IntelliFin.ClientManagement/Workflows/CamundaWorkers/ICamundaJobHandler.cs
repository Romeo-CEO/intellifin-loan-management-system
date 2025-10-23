using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Base interface for Camunda job handlers
/// Implementations process specific BPMN service tasks
/// </summary>
public interface ICamundaJobHandler
{
    /// <summary>
    /// Handles a job from Camunda workflow
    /// </summary>
    /// <param name="jobClient">Zeebe job client for completing/failing jobs</param>
    /// <param name="job">Job details including variables and metadata</param>
    /// <returns>Task completing when job processing finishes</returns>
    Task HandleJobAsync(IJobClient jobClient, IJob job);

    /// <summary>
    /// Gets the topic name this worker subscribes to
    /// Example: "client.kyc.verify-documents"
    /// </summary>
    string GetTopicName();

    /// <summary>
    /// Gets the job type identifier for this worker
    /// Example: "io.intellifin.kyc.verify"
    /// </summary>
    string GetJobType();
}
