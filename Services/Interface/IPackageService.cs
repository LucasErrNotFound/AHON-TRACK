using System.Collections.Generic;
using AHON_TRACK.ViewModels;

namespace AHON_TRACK.Services.Interface;

public interface IPackageService
{
    List<Package> GetPackages();
    void AddPackage(Package package);
    void RemovePackage(Package package);
    void UpdatePackage(Package oldPackage, Package newPackage);
    event System.Action? PackagesChanged;
}