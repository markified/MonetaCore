using MonetaCore.Models;
using MonetaCore.ViewModels;

namespace MonetaCore.Tests;

public class SystemConfigurationViewModelTests
{
    [Fact]
    public void ApplyTo_WhenNoRotationOrClear_KeepsExistingSecrets()
    {
        var settings = new SystemConfigurationSettings
        {
            PayMongoSecretKey = "paymongo-secret-existing",
            IntegrationApiKey = "api_key_existing",
            IntegrationBearerToken = "bearer_existing"
        };

        var model = SystemConfigurationViewModel.FromSettings(settings);
        model.SessionTimeoutMinutes = 600;

        model.ApplyTo(settings, "superadmin@local");

        Assert.Equal("paymongo-secret-existing", settings.PayMongoSecretKey);
        Assert.Equal("api_key_existing", settings.IntegrationApiKey);
        Assert.Equal("bearer_existing", settings.IntegrationBearerToken);
        Assert.Equal(600, settings.SessionTimeoutMinutes);
    }

    [Fact]
    public void ApplyTo_RotatesAndClearsSecrets_AsRequested()
    {
        var settings = new SystemConfigurationSettings
        {
            PayMongoSecretKey = "paymongo-secret-existing",
            IntegrationApiKey = "api_key_existing",
            IntegrationBearerToken = "bearer_existing"
        };

        var model = SystemConfigurationViewModel.FromSettings(settings);
        model.NewPayMongoSecretKey = "paymongo-secret-rotated";
        model.ClearIntegrationApiKey = true;
        model.NewIntegrationBearerToken = "bearer_rotated";

        model.ApplyTo(settings, "superadmin@local");

        Assert.Equal("paymongo-secret-rotated", settings.PayMongoSecretKey);
        Assert.Equal(string.Empty, settings.IntegrationApiKey);
        Assert.Equal("bearer_rotated", settings.IntegrationBearerToken);
    }

    [Fact]
    public void ApplyTo_ClearTakesPriorityOverNewPayMongoSecret()
    {
        var settings = new SystemConfigurationSettings
        {
            PayMongoSecretKey = "paymongo-secret-existing"
        };

        var model = SystemConfigurationViewModel.FromSettings(settings);
        model.NewPayMongoSecretKey = "paymongo-secret-rotated";
        model.ClearPayMongoSecretKey = true;

        model.ApplyTo(settings, "superadmin@local");

        Assert.Equal(string.Empty, settings.PayMongoSecretKey);
    }
}
