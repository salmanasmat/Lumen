using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly IProfileService _profileService;

    [ObservableProperty]
    private string _title = "Optimization Profiles";

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string _statusText = "Select a profile to run end-to-end optimization.";

    [ObservableProperty]
    private ObservableCollection<LumenProfile> _profiles = new();

    [ObservableProperty]
    private LumenProfile? _selectedProfile;

    public ProfilesViewModel(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [RelayCommand]
    private async Task LoadProfilesAsync()
    {
        IsApplying = true;
        StatusText = "Loading profiles...";

        try
        {
            Profiles.Clear();
            var defaultProfile = await _profileService.GetDefaultProfileAsync();
            Profiles.Add(defaultProfile);

            var customProfiles = await _profileService.GetCustomProfilesAsync();
            foreach (var cp in customProfiles)
            {
                Profiles.Add(cp);
            }

            SelectedProfile = defaultProfile;
            StatusText = $"Loaded {Profiles.Count} optimization profile(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load profiles: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }

    [RelayCommand]
    private async Task ApplyProfileAsync(LumenProfile profile)
    {
        var targetProfile = profile ?? SelectedProfile;
        if (targetProfile == null) return;

        IsApplying = true;
        StatusText = $"Starting batch execution for profile '{targetProfile.Name}'...";

        try
        {
            var progress = new Progress<string>(msg => StatusText = msg);
            var (res, msg) = await _profileService.ApplyProfileAsync(targetProfile, progress);
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Profile execution failed: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }
}
