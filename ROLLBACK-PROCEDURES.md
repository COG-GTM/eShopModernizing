# Strangler Fig Migration Rollback Procedures

## Quick Rollback (Emergency)

1. **Immediate Traffic Rollback**
   ```bash
   # Restore previous nginx configuration
   cp nginx-backups/nginx-[timestamp].conf nginx.conf
   sudo nginx -s reload
   ```

2. **Verify Legacy System Health**
   ```bash
   curl http://localhost:5001/health
   ```

## Gradual Rollback

1. **Reduce .NET Core Traffic Gradually**
   - 100% → 75%: `./migrate-traffic.sh 75`
   - 75% → 50%: `./migrate-traffic.sh 50`
   - 50% → 25%: `./migrate-traffic.sh 25`
   - 25% → 0%: Restore original nginx.conf

2. **Monitor During Rollback**
   - Check error rates in Application Insights
   - Monitor response times
   - Verify database consistency

## Common Rollback Scenarios

### High Error Rate
- Threshold: >5% error rate for 5 minutes
- Action: Immediate rollback to previous traffic percentage

### Performance Degradation
- Threshold: >2x response time increase
- Action: Gradual rollback with monitoring

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
| 500 Errors | >1% | Immediate rollback |
| Response Time | >5s avg | Gradual rollback |
| Azure Service Down | Critical | Disable Azure features |
| Database Issues | Critical | Immediate rollback |
| Authentication Failure | High | Rollback auth config |
