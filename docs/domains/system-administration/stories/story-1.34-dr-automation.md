# Story 1.34: Disaster Recovery Runbook Automation

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.34 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 6: Advanced Observability |
| **Sprint** | Sprint 12 |
| **Story Points** | 13 |
| **Estimated Effort** | 10-15 days |
| **Priority** | P0 (Critical) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Camunda workflows, Azure Site Recovery, backup systems |
| **Blocks** | BoZ audit compliance, business continuity |

---

## User Story

**As a** System Administrator,  
**I want** automated disaster recovery testing and runbook execution,  
**so that** I can validate RPO/RTO targets and meet Bank of Zambia audit requirements.

---

## Business Value

- **Regulatory Compliance**: Meets BoZ requirements for DR testing and documentation
- **Business Continuity**: Validated DR procedures ensure rapid recovery
- **Risk Mitigation**: Regular testing identifies gaps before real disasters
- **Audit Readiness**: Automated evidence generation simplifies audits
- **Confidence**: Proven recovery capability reduces business risk
- **Cost Optimization**: Automated testing reduces manual effort by 80%

---

## Acceptance Criteria

### AC1: Quarterly DR Testing Automation
**Given** DR procedures need quarterly validation  
**When** running automated DR test  
**Then**:
- Camunda process: `disaster-recovery-test.bpmn`
- Test scenarios: Database failover, AKS cluster recovery, Vault restore, complete site failover
- Automated validation: Data integrity, service availability, authentication, audit chain
- Test duration tracked: < 4 hours for full DR test
- Results stored with evidence: Logs, screenshots, metrics

### AC2: RPO/RTO Validation
**Given** SLA targets: RPO=15min, RTO=4hrs  
**When** DR test executes  
**Then**:
- RPO measurement: Data loss from last backup
- RTO measurement: Time from failure to service restoration
- Automated assertions: RPO < 15min, RTO < 4hrs
- Detailed timeline: Failure detection → Backup restore → Service start → Health check
- Pass/fail results with deviation analysis

### AC3: Backup Restore Verification
**Given** Backups must be restorable  
**When** weekly automated restore test  
**Then**:
- Restore to isolated test environment
- Validate: Database schema, data integrity, row counts, referential integrity
- Test queries: Sample loan data retrieval
- Cleanup after test
- Results tracked: Success rate, restore time, data validation

### AC4: BoZ Audit Evidence Generation
**Given** Regulators require DR testing evidence  
**When** DR test completes  
**Then**:
- Evidence package generated: Executive summary, test plan, test results, timeline, screenshots, logs
- PDF report with digital signature
- Stored in compliance repository: 7 years retention
- Report includes: Test date, scenarios, pass/fail, RPO/RTO, action items
- Accessible via Admin UI with RBAC

---

## Technical Implementation Details

### Camunda DR Test Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  id="disaster-recovery-test">
  
  <bpmn:process id="DisasterRecoveryTest" name="Automated DR Test" isExecutable="true">
    
    <bpmn:startEvent id="StartTest" name="Initiate DR Test">
      <bpmn:timerEventDefinition>
        <bpmn:timeCycle>0 0 2 1 1/3 ?</bpmn:timeCycle> <!-- Quarterly at 2am -->
      </bpmn:timerEventDefinition>
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    
    <bpmn:serviceTask id="PrepareTestEnvironment" name="Prepare Isolated Test Environment" 
                      camunda:delegateExpression="${prepareTestEnvDelegate}">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="SimulateFailure" name="Simulate Disaster Scenario" 
                      camunda:delegateExpression="${simulateFailureDelegate}">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="RestoreFromBackup" name="Restore from Latest Backup" 
                      camunda:delegateExpression="${restoreBackupDelegate}">
      <bpmn:incoming>Flow_3</bpmn:incoming>
      <bpmn:outgoing>Flow_4</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="MeasureRPO" name="Measure RPO" 
                      camunda:delegateExpression="${measureRPODelegate}">
      <bpmn:incoming>Flow_4</bpmn:incoming>
      <bpmn:outgoing>Flow_5</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="RestartServices" name="Restart Critical Services" 
                      camunda:delegateExpression="${restartServicesDelegate}">
      <bpmn:incoming>Flow_5</bpmn:incoming>
      <bpmn:outgoing>Flow_6</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="MeasureRTO" name="Measure RTO" 
                      camunda:delegateExpression="${measureRTODelegate}">
      <bpmn:incoming>Flow_6</bpmn:incoming>
      <bpmn:outgoing>Flow_7</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="ValidateDataIntegrity" name="Validate Data Integrity" 
                      camunda:delegateExpression="${validateDataDelegate}">
      <bpmn:incoming>Flow_7</bpmn:incoming>
      <bpmn:outgoing>Flow_8</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="ValidateAuditChain" name="Validate Audit Chain" 
                      camunda:delegateExpression="${validateAuditDelegate}">
      <bpmn:incoming>Flow_8</bpmn:incoming>
      <bpmn:outgoing>Flow_9</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:exclusiveGateway id="Gateway_TestsPassed" name="All Tests Passed?">
      <bpmn:incoming>Flow_9</bpmn:incoming>
      <bpmn:outgoing>Flow_Pass</bpmn:outgoing>
      <bpmn:outgoing>Flow_Fail</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Success Path -->
    <bpmn:serviceTask id="GenerateEvidencePackage" name="Generate Audit Evidence" 
                      camunda:delegateExpression="${generateEvidenceDelegate}">
      <bpmn:incoming>Flow_Pass</bpmn:incoming>
      <bpmn:outgoing>Flow_10</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Failure Path -->
    <bpmn:serviceTask id="NotifyFailure" name="Notify Stakeholders of Failure" 
                      camunda:delegateExpression="${notifyFailureDelegate}">
      <bpmn:incoming>Flow_Fail</bpmn:incoming>
      <bpmn:outgoing>Flow_11</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:userTask id="ReviewFailure" name="Review Failed DR Test" 
                   camunda:assignee="${drCoordinator}">
      <bpmn:incoming>Flow_11</bpmn:incoming>
      <bpmn:outgoing>Flow_12</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Cleanup -->
    <bpmn:serviceTask id="CleanupTestEnvironment" name="Cleanup Test Environment" 
                      camunda:delegateExpression="${cleanupTestEnvDelegate}">
      <bpmn:incoming>Flow_10</bpmn:incoming>
      <bpmn:incoming>Flow_12</bpmn:incoming>
      <bpmn:outgoing>Flow_13</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:endEvent id="EndTest" name="DR Test Complete">
      <bpmn:incoming>Flow_13</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_1" sourceRef="StartTest" targetRef="PrepareTestEnvironment" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="PrepareTestEnvironment" targetRef="SimulateFailure" />
    <bpmn:sequenceFlow id="Flow_Pass" sourceRef="Gateway_TestsPassed" targetRef="GenerateEvidencePackage">
      <bpmn:conditionExpression>${testsPassed == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Fail" sourceRef="Gateway_TestsPassed" targetRef="NotifyFailure">
      <bpmn:conditionExpression>${testsPassed == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
  </bpmn:process>
</bpmn:definitions>
```

### DR Test Service

```csharp
public class DisasterRecoveryTestService
{
    private readonly IBackupService _backupService;
    private readonly IKubernetesClient _k8sClient;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DisasterRecoveryTestService> _logger;

    public async Task<DrTestResult> ExecuteDrTestAsync(DrTestScenario scenario, CancellationToken ct)
    {
        var testId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting DR test {TestId} for scenario {Scenario}", testId, scenario);
            
            // 1. Prepare isolated test environment
            var testEnv = await PrepareTestEnvironmentAsync(scenario, ct);
            
            // 2. Simulate disaster
            var failureTime = DateTime.UtcNow;
            await SimulateDisasterAsync(testEnv, scenario, ct);
            
            // 3. Restore from backup
            var restoreStartTime = DateTime.UtcNow;
            var backup = await _backupService.GetLatestBackupAsync(ct);
            await _backupService.RestoreAsync(backup.Id, testEnv.ConnectionString, ct);
            var restoreEndTime = DateTime.UtcNow;
            
            // 4. Measure RPO (data loss)
            var rpo = await MeasureRpoAsync(testEnv, failureTime, ct);
            
            // 5. Restart services
            await RestartServicesAsync(testEnv, ct);
            
            // 6. Measure RTO (time to recovery)
            var rto = restoreEndTime - failureTime;
            
            // 7. Validate data integrity
            var dataValidation = await ValidateDataIntegrityAsync(testEnv, ct);
            
            // 8. Validate audit chain
            var auditValidation = await _auditService.ValidateChainIntegrityAsync(testEnv.ConnectionString, ct);
            
            // 9. Determine pass/fail
            var passed = rpo.TotalMinutes < 15 && rto.TotalHours < 4 && dataValidation && auditValidation;
            
            var result = new DrTestResult
            {
                TestId = testId,
                Scenario = scenario,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Rpo = rpo,
                Rto = rto,
                DataIntegrityValid = dataValidation,
                AuditChainValid = auditValidation,
                Passed = passed
            };
            
            // 10. Generate evidence package
            if (passed)
            {
                await GenerateEvidencePackageAsync(result, ct);
            }
            
            return result;
        }
        finally
        {
            // Always cleanup test environment
            await CleanupTestEnvironmentAsync(testId, ct);
        }
    }

    private async Task<TimeSpan> MeasureRpoAsync(TestEnvironment env, DateTime failureTime, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(env.ConnectionString);
        await conn.OpenAsync(ct);
        
        var cmd = new NpgsqlCommand(@"
            SELECT MAX(created_at) 
            FROM audit_log 
            WHERE created_at < @failureTime", conn);
        cmd.Parameters.AddWithValue("failureTime", failureTime);
        
        var lastAuditTime = (DateTime)await cmd.ExecuteScalarAsync(ct);
        return failureTime - lastAuditTime;
    }

    private async Task<bool> ValidateDataIntegrityAsync(TestEnvironment env, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(env.ConnectionString);
        await conn.OpenAsync(ct);
        
        // Check referential integrity
        var integrityCmd = new NpgsqlCommand(@"
            SELECT COUNT(*) FROM information_schema.table_constraints 
            WHERE constraint_type = 'FOREIGN KEY'
            AND constraint_name NOT IN (
                SELECT constraint_name FROM information_schema.constraint_column_usage
                WHERE table_name IN (SELECT table_name FROM information_schema.tables)
            )", conn);
        var violations = (long)await integrityCmd.ExecuteScalarAsync(ct);
        
        if (violations > 0)
        {
            _logger.LogError("Referential integrity violations detected: {Count}", violations);
            return false;
        }
        
        // Validate sample data
        var sampleCmd = new NpgsqlCommand("SELECT COUNT(*) FROM loans WHERE status = 'ACTIVE'", conn);
        var activeLoanCount = (long)await sampleCmd.ExecuteScalarAsync(ct);
        
        _logger.LogInformation("Validated {Count} active loans after restore", activeLoanCount);
        return activeLoanCount > 0;
    }
}
```

### Evidence Package Generation

```csharp
public class DrEvidencePackageGenerator
{
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IDigitalSignatureService _signatureService;
    private readonly IStorageService _storageService;

    public async Task<string> GenerateEvidencePackageAsync(DrTestResult result, CancellationToken ct)
    {
        var reportData = new DrTestReport
        {
            TestDate = result.StartTime,
            Scenario = result.Scenario.ToString(),
            ExecutiveSummary = GenerateExecutiveSummary(result),
            TestPlan = GetTestPlan(result.Scenario),
            Results = new DrTestResultsSummary
            {
                Rpo = $"{result.Rpo.TotalMinutes:F2} minutes",
                RpoTarget = "< 15 minutes",
                RpoStatus = result.Rpo.TotalMinutes < 15 ? "PASS" : "FAIL",
                Rto = $"{result.Rto.TotalHours:F2} hours",
                RtoTarget = "< 4 hours",
                RtoStatus = result.Rto.TotalHours < 4 ? "PASS" : "FAIL",
                DataIntegrity = result.DataIntegrityValid ? "PASS" : "FAIL",
                AuditChain = result.AuditChainValid ? "PASS" : "FAIL"
            },
            Timeline = GenerateTimeline(result),
            ActionItems = GenerateActionItems(result)
        };
        
        // Generate PDF
        var pdfBytes = await _pdfGenerator.GeneratePdfAsync("DR-Test-Report", reportData, ct);
        
        // Digitally sign PDF
        var signedPdfBytes = await _signatureService.SignPdfAsync(pdfBytes, ct);
        
        // Store in compliance repository with 7-year retention
        var storageKey = $"dr-evidence/{result.TestDate:yyyy-MM}/dr-test-{result.TestId}.pdf";
        await _storageService.UploadAsync(storageKey, signedPdfBytes, new StorageMetadata
        {
            RetentionPolicy = TimeSpan.FromDays(365 * 7),
            Classification = "Regulatory",
            Tags = new Dictionary<string, string>
            {
                ["TestId"] = result.TestId.ToString(),
                ["TestDate"] = result.StartTime.ToString("O"),
                ["Passed"] = result.Passed.ToString()
            }
        }, ct);
        
        return storageKey;
    }

    private string GenerateExecutiveSummary(DrTestResult result)
    {
        return result.Passed
            ? $"Disaster Recovery test executed successfully on {result.StartTime:yyyy-MM-dd}. " +
              $"All recovery objectives met: RPO {result.Rpo.TotalMinutes:F2} minutes (target < 15 min), " +
              $"RTO {result.Rto.TotalHours:F2} hours (target < 4 hrs). " +
              "Data integrity and audit chain validated. System ready for production recovery."
            : $"Disaster Recovery test executed on {result.StartTime:yyyy-MM-dd} with failures. " +
              $"Review required for: {GetFailureReasons(result)}. " +
              "Action items created for remediation.";
    }
}
```

---

## Database Schema

```sql
-- DR test results
CREATE TABLE dr_test_results (
    test_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    test_date TIMESTAMPTZ NOT NULL,
    scenario VARCHAR(50) NOT NULL,
    rpo_minutes DECIMAL(10,2) NOT NULL,
    rpo_target_minutes DECIMAL(10,2) NOT NULL DEFAULT 15.0,
    rpo_passed BOOLEAN NOT NULL,
    rto_hours DECIMAL(10,2) NOT NULL,
    rto_target_hours DECIMAL(10,2) NOT NULL DEFAULT 4.0,
    rto_passed BOOLEAN NOT NULL,
    data_integrity_valid BOOLEAN NOT NULL,
    audit_chain_valid BOOLEAN NOT NULL,
    overall_passed BOOLEAN NOT NULL,
    evidence_package_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL
);

CREATE INDEX idx_dr_test_date ON dr_test_results(test_date DESC);
CREATE INDEX idx_dr_test_passed ON dr_test_results(overall_passed);
```

---

## Integration Verification

### IV1: DR Test Execution
**Verification Steps**:
1. Trigger quarterly DR test
2. Monitor Camunda workflow progress
3. Verify backup restore completes
4. Check RPO/RTO measurements
5. Validate evidence package generated

**Success Criteria**:
- Test completes in < 4 hours
- RPO < 15 minutes
- RTO < 4 hours
- Evidence package available in Admin UI

### IV2: Backup Restore Validation
**Verification Steps**:
1. Trigger weekly restore test
2. Verify data restored to test environment
3. Run sample queries
4. Check referential integrity
5. Confirm cleanup executed

**Success Criteria**:
- Restore completes successfully
- All data integrity checks pass
- Test environment cleaned up

### IV3: BoZ Evidence Generation
**Verification Steps**:
1. Review generated PDF report
2. Verify digital signature valid
3. Check 7-year retention policy
4. Confirm report accessible via Admin UI
5. Validate report completeness

**Success Criteria**:
- Report includes all required sections
- Digital signature valid
- Stored with proper retention
- RBAC enforced on access

---

## Testing Strategy

### Integration Tests

```csharp
[Fact]
public async Task ExecuteDrTest_WhenBackupRestored_MeasuresRpoRtoAccurately()
{
    var service = CreateDrTestService();
    var scenario = DrTestScenario.DatabaseFailover;

    var result = await service.ExecuteDrTestAsync(scenario, CancellationToken.None);

    Assert.NotNull(result);
    Assert.True(result.Rpo.TotalMinutes < 15);
    Assert.True(result.Rto.TotalHours < 4);
    Assert.True(result.Passed);
}

[Fact]
public async Task GenerateEvidencePackage_WhenTestPassed_CreatesSignedPdf()
{
    var generator = CreateEvidenceGenerator();
    var testResult = CreatePassedTestResult();

    var storageKey = await generator.GenerateEvidencePackageAsync(testResult, CancellationToken.None);

    Assert.NotNull(storageKey);
    Assert.Contains("dr-evidence", storageKey);
    
    // Verify file exists in storage
    var fileExists = await _storageService.ExistsAsync(storageKey, CancellationToken.None);
    Assert.True(fileExists);
}
```

---

## Admin UI Components

### DR Dashboard

```tsx
export const DrDashboard: React.FC = () => {
  const { data: testResults } = useQuery('dr-tests', fetchDrTestResults);
  const { mutate: runTest } = useMutation(triggerDrTest);

  return (
    <div className="dr-dashboard">
      <h2>Disaster Recovery Testing</h2>
      
      <div className="dr-stats">
        <StatCard
          title="Last Test Date"
          value={format(testResults[0]?.testDate, 'yyyy-MM-dd')}
        />
        <StatCard
          title="Last RPO"
          value={`${testResults[0]?.rpoMinutes} min`}
          status={testResults[0]?.rpoPassed ? 'success' : 'error'}
        />
        <StatCard
          title="Last RTO"
          value={`${testResults[0]?.rtoHours} hrs`}
          status={testResults[0]?.rtoPassed ? 'success' : 'error'}
        />
        <StatCard
          title="Success Rate"
          value={`${calculateSuccessRate(testResults)}%`}
        />
      </div>

      <Button onClick={() => runTest()}>Run DR Test Now</Button>

      <div className="test-history">
        <h3>Test History</h3>
        <Table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Scenario</th>
              <th>RPO</th>
              <th>RTO</th>
              <th>Status</th>
              <th>Evidence</th>
            </tr>
          </thead>
          <tbody>
            {testResults.map(test => (
              <tr key={test.testId}>
                <td>{format(test.testDate, 'yyyy-MM-dd HH:mm')}</td>
                <td>{test.scenario}</td>
                <td className={test.rpoPassed ? 'pass' : 'fail'}>
                  {test.rpoMinutes} min
                </td>
                <td className={test.rtoPassed ? 'pass' : 'fail'}>
                  {test.rtoHours} hrs
                </td>
                <td>
                  <Badge variant={test.overallPassed ? 'success' : 'error'}>
                    {test.overallPassed ? 'PASSED' : 'FAILED'}
                  </Badge>
                </td>
                <td>
                  <a href={test.evidencePackageUrl} download>
                    Download Report
                  </a>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
    </div>
  );
};
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| DR test disrupts production | Production outage | Low | Isolated test environment. No production dependencies. |
| Test failures not addressed | Real DR fails | Medium | Mandatory review of failures. Action items tracked. Quarterly cadence. |
| Evidence package incomplete | Audit failure | Low | Template validation. Automated checks. Peer review. |
| RPO/RTO targets missed | SLA breach | Medium | Alert on missed targets. Root cause analysis. Incremental improvements. |

---

## Definition of Done

- [ ] Camunda DR test workflow deployed
- [ ] Quarterly automated test scheduled
- [ ] Weekly backup restore test scheduled
- [ ] RPO/RTO measurement accurate
- [ ] Data integrity validation implemented
- [ ] Evidence package generator functional
- [ ] PDF digital signature working
- [ ] 7-year retention policy enforced
- [ ] Admin UI DR dashboard complete
- [ ] Integration tests: DR test end-to-end
- [ ] Documentation: DR runbook, test procedures
- [ ] BoZ audit evidence reviewed by compliance team

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Previous Story**: [Story 1.33: Automated Alerting and Incident Response](./story-1.33-automated-alerting.md)
