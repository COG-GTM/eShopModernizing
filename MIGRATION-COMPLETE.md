# eShop Strangler Fig Migration - Completion Documentation

## Migration Overview

The strangler fig migration from .NET Framework to .NET Core has been completed successfully. This document outlines the final architecture and operational procedures.

## Final Architecture

### Traffic Routing
- **API Endpoints** (`/api/*`): 100% .NET Core (port 5002)
- **Catalog Endpoints** (`/Catalog/*`): 100% .NET Core (port 5002)
- **Health Checks** (`/api/health*`): .NET Core (port 5002)
- **Legacy Endpoints**: Modernized .NET Framework (port 5001)

### Service Ports
- **Legacy .NET Framework**: localhost:5000 (decommissioned)
- **Modernized .NET Framework**: localhost:5001 (remaining functionality)
- **.NET Core**: localhost:5002 (primary catalog service)

## Azure Integrations

All Azure services are fully integrated and tested:

### Feature Flags
- `UseMockData`: Toggle between mock and real data
- `UseAzureStorage`: Enable Azure Blob Storage for images
- `UseAzureManagedIdentity`: Use Managed Identity for authentication
- `UseAzureActiveDirectory`: Enable Azure AD authentication

### Services
- **Azure Key Vault**: Configuration management
- **Azure Blob Storage**: Image storage and management
- **Azure Application Insights**: Telemetry and monitoring
- **Azure SQL Database**: Data persistence with Managed Identity
- **Azure Active Directory**: Authentication and authorization

## Monitoring and Health Checks

### Health Endpoints
- `/api/health`: Basic health check
- `/api/health/detailed`: Comprehensive service status including Azure services

### Application Insights
- Custom telemetry for migration tracking
- Performance monitoring and comparison
- Error tracking and alerting
- Live metrics stream enabled

## Operational Procedures

### Deployment
1. Build .NET Core application
2. Update configuration in Azure Key Vault
3. Deploy to target environment
4. Verify health checks
5. Monitor Application Insights

### Rollback (if needed)
See [ROLLBACK-PROCEDURES.md](./ROLLBACK-PROCEDURES.md) for detailed rollback procedures.

### Performance Monitoring
- Monitor response times via Application Insights
- Track error rates and exceptions
- Compare performance metrics with legacy system
- Set up alerts for critical thresholds

## Traffic Migration Strategy

The migration uses nginx upstream weighting (consistent-hash, client-sticky) for gradual traffic
distribution. The operator procedure (advancing, what to watch, rollback) is
[CANARY-CUTOVER-RUNBOOK.md](./CANARY-CUTOVER-RUNBOOK.md).

### Migration Phases
0. **Baseline**: `nginx-0percent.conf` - 100% to .NET Framework (bootstrap and full-rollback target)
1. **25% Phase**: `nginx-25percent.conf` - 25% to .NET Core, 75% to .NET Framework
2. **50% Phase**: `nginx-50percent.conf` - 50% to .NET Core, 50% to .NET Framework
3. **75% Phase**: `nginx-75percent.conf` - 75% to .NET Core, 25% to .NET Framework
4. **100% Phase**: `nginx-100percent.conf` - 100% to .NET Core

### Migration Script
Use `canary/cutover.sh advance` to move one phase forward. It runs preflight checks, validates and
reloads nginx, soaks with error-rate/latency/failover gates, and rolls back automatically on a
breach. `canary/cutover.sh rollback` moves back. `./migrate-traffic.sh {0|25|50|75|100}` is kept as a
deprecated wrapper.

## Legacy System Decommission

The legacy .NET Framework system (port 5000) can now be safely decommissioned:

1. **Verify Traffic**: Ensure no traffic is routed to port 5000
2. **Data Migration**: Confirm all data is accessible via .NET Core
3. **Remove Infrastructure**: Decommission legacy containers/services
4. **Update Documentation**: Remove references to legacy system
5. **Clean Up**: Remove legacy code and configurations

## Feature Validation

All feature flag combinations have been tested:

### Test Configurations
1. **Development**: Mock data, no Azure services
2. **Staging**: Azure Storage + Managed Identity, no AAD
3. **Production**: All Azure services enabled

### Validation Script
Use `./validate-features.ps1` to test all feature flag combinations automatically.

## Next Steps

1. **Performance Optimization**: Fine-tune .NET Core application based on production metrics
2. **Feature Enhancement**: Add new features leveraging .NET Core capabilities
3. **Monitoring Enhancement**: Expand telemetry and monitoring capabilities
4. **Security Review**: Conduct security assessment of new architecture

## Success Metrics

The migration is considered successful based on:
- ✅ 100% traffic routed to .NET Core for catalog functionality
- ✅ All Azure integrations working correctly
- ✅ Performance metrics meet or exceed legacy system
- ✅ Error rates remain below 1%
- ✅ All feature flags tested and validated
- ✅ Comprehensive monitoring and alerting in place
- ✅ Rollback procedures documented and tested

## Configuration Files

### Nginx Configurations
- `nginx.conf`: Pre-runbook configuration (unmarked 25% weights), kept for reference and not managed by `canary/cutover.sh`
- `nginx-0percent.conf`: Baseline / full-rollback stage (100% .NET Framework)
- `nginx-25percent.conf`: 25% migration phase
- `nginx-50percent.conf`: 50% migration phase
- `nginx-75percent.conf`: 75% migration phase
- `nginx-100percent.conf`: 100% migration phase

### Docker Compose
- `docker-compose.yml`: Service definitions including .NET Core service
- `docker-compose.override.yml`: Environment-specific configurations

### Scripts
- `canary/cutover.sh`: Scripted canary cutover (advance / rollback / watch / report), see CANARY-CUTOVER-RUNBOOK.md
- `migrate-traffic.sh`: Deprecated wrapper around `canary/cutover.sh`
- `validate-features.ps1`: Feature flag validation script

## Troubleshooting

### Common Issues
1. **Health Check Failures**: Check Azure service connectivity
2. **Image Upload Issues**: Verify Azure Storage configuration
3. **Authentication Problems**: Check Azure AD settings
4. **Database Connectivity**: Verify Managed Identity configuration

### Monitoring
- Application Insights dashboard for real-time metrics
- Health check endpoints for service status
- `canary/cutover.sh report` (reads `/var/log/nginx/eshop-canary.log`) for traffic distribution verification
- Database performance counters

## Support Information

For issues or questions:
1. Check Application Insights for error details
2. Review health check endpoints
3. Consult rollback procedures if needed
4. Contact DevOps team for infrastructure issues
