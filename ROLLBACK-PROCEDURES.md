# Strangler Fig Migration Rollback Procedures

`/Catalog/` traffic rollback is scripted. The full procedure, including what to watch and the
automatic gates, is in [CANARY-CUTOVER-RUNBOOK.md](CANARY-CUTOVER-RUNBOOK.md). This page is the
quick reference.

## Quick Rollback (Emergency)

1. **Immediate Traffic Rollback: all `/Catalog/` traffic to the .NET Framework app**
   ```bash
   sudo canary/cutover.sh rollback --to 0 --yes --reason "INC-<id>"
   ```
   If the script is unavailable or fails:
   ```bash
   sudo cp nginx-0percent.conf /etc/nginx/conf.d/eshop.conf   # or the newest canary/state/backups/*.conf
   sudo nginx -t && sudo nginx -s reload
   ```

2. **Verify**
   ```bash
   curl -sI http://localhost/Catalog/ | grep -i x-canary-stage   # expect: X-Canary-Stage: 0
   curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/
   canary/cutover.sh status
   ```

## Gradual Rollback

1. **Reduce .NET Core Traffic One Stage at a Time**
   ```bash
   sudo canary/cutover.sh rollback            # 100 -> 75 -> 50 -> 25 -> 0, one step per run
   sudo canary/cutover.sh rollback --to 25    # or straight to a specific stage
   ```
   Each rollback checks that the target backends are healthy, backs up the live config, runs
   `nginx -t`, reloads, confirms the `X-Canary-Stage` header, and then watches the gates for 120 s.

2. **Monitor During Rollback**
   - `canary/cutover.sh report` for the split, 5xx and p95 per backend
   - Check error rates in Application Insights
   - Monitor response times
   - Verify database consistency

3. **Restore an exact earlier config** (e.g. after a hand edit)
   ```bash
   sudo canary/cutover.sh restore --file canary/state/backups/eshop-<utc>-stage<N>.conf
   ```

## Common Rollback Scenarios

### High Error Rate
- Automatic gate: .NET Core 5xx > 1% (`MAX_5XX_PCT`) over the 5-minute window during `advance`
  or `watch --auto-rollback` rolls back one stage
- Manual threshold: >1% error rate for 5 minutes in Application Insights
- Action: Immediate rollback to the previous stage (`canary/cutover.sh rollback`)

### Performance Degradation
- Automatic gate: .NET Core p95 > 2x the .NET Framework p95 (`MAX_P95_RATIO`), or > 5 s absolute
- Manual threshold: >2x response time increase
- Action: Gradual rollback with monitoring

### .NET Core Unavailable
- nginx fails over to the .NET Framework app, so users may not see errors
- Automatic gates: failover rate > 1%, or .NET Core share more than 15 points below the stage
- Action: `canary/cutover.sh rollback` (or `--to 0`), then investigate the Core host

### Azure Service Issues
- Check Azure service health
- Verify feature flags are correctly configured
- Consider disabling Azure integrations temporarily

## Post-Rollback Actions

1. Analyze logs and telemetry data
2. Identify root cause of issues
3. Plan remediation steps
4. Schedule next migration attempt

## Rollback Validation

After any rollback:
1. Verify all catalog functionality works
2. Check database connectivity
3. Validate image upload/download
4. Monitor error rates for 30 minutes
5. Confirm user authentication works

## Emergency Contacts

- DevOps Team: [Contact Information]
- Database Administrator: [Contact Information]
- Azure Support: [Support Information]

## Rollback Decision Matrix

| Issue Type | Severity | Action |
|------------|----------|--------|
| 500 Errors | >1% | Immediate rollback (`canary/cutover.sh rollback`) |
| Data correctness | Any | Rollback to 0 (`canary/cutover.sh rollback --to 0`) |
| Response Time | >5s avg | Gradual rollback |
| Azure Service Down | Critical | Disable Azure features |
| Database Issues | Critical | Immediate rollback |
| Authentication Failure | High | Rollback auth config |
