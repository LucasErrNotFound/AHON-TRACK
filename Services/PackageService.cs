using System;
using System.Collections.Generic;
using System.Linq;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;

namespace AHON_TRACK.Services;

public class PackageService : IPackageService
{
    private readonly List<Package> _packages = [];
    private readonly HashSet<string> _deletedPackageIds = [];
    private readonly Dictionary<Package, string> _originalTitles = [];
    
    public event Action? PackagesChanged;

    public List<Package> GetPackages()
    {
        var defaultPackages = GetDefaultPackages()
            .Where(p => !_deletedPackageIds.Contains(p.Title))
            .ToList();
        defaultPackages.AddRange(_packages);
        
        return defaultPackages;
    }

    private List<Package> GetDefaultPackages()
    {
        return
        [
            new Package
            {
                Title = "Free Trial",
                Description = "Try any class or gym session-no charge",
                Price = 0,
                PriceUnit = "/One-time only",
                Features = 
                [
                    "Risk-free first session",
                    "Explore the gym or class",
                    "Meet our trainers",
                    "Decide before committing",
                    "No hidden charges"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Walk-In",
                Description = "Pay per session with no commitment",
                Price = 150,
                PriceUnit = "/Session",
                Features = 
                [
                    "Unlimited time",
                    "Perfect for casual visits",
                    "Pay only when you train",
                    "No membership needed",
                    "Great for one-time guests"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Monthly Membership",
                Description = "Unlimited gym access for 30 days",
                Price = 500,
                PriceUnit = "/Month",
                Features = 
                [
                    "Unlimited gym sessions",
                    "Best value for regulars",
                    "Discounted charges",
                    "30 days of full access",
                    "Unli health checkups"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Boxing",
                Description = "High-intensity training focused on boxing",
                Price = 450,
                PriceUnit = "/Session",
                Features = 
                [
                    "Boosts cardio and strength",
                    "Learn real boxing skills",
                    "Stress relieving workouts",
                    "Burns high calories fast",
                    "Builds confidence and discipline"
                ],
                IsDiscountChecked = true,
                DiscountValue = 100,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Muay Thai",
                Description = "Training for strength and endurance",
                Price = 500,
                PriceUnit = "/Session",
                Features = 
                [
                    "Full body conditioning",
                    "Improves flexibility and balance",
                    "Learn about self-defense",
                    "Culturally rich experience",
                    "Great for strength and stamina"
                ],
                IsDiscountChecked = true,
                DiscountValue = 100,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "CrossFit",
                Description = "Functional workouts with high-intensity moves",
                Price = 300,
                PriceUnit = "/Session",
                Features = 
                [
                    "Builds functional strength",
                    "Constantly varied workouts",
                    "Time efficient and intense",
                    "Encourages community support",
                    "Suitable for all fitness levels"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Personal Training",
                Description = "One-on-one session to reach your fitness goal",
                Price = 200,
                PriceUnit = "/Session",
                Features = 
                [
                    "Personalized workout plans",
                    "One-on-one coaching",
                    "Faster progress tracking",
                    "Motivation and accountability",
                    "Focus on your specific goals"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Thai Massage",
                Description = "Massage therapy for relaxation and healing",
                Price = 350,
                PriceUnit = "/Session",
                Features = 
                [
                    "Relieves muscle tension and stiffness",
                    "Enhances flexibility and circulation",
                    "Promotes deep relaxation",
                    "Reduces stress and fatigue",
                    "Supports overall wellness"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            }
        ];
    }

    public void AddPackage(Package package)
    {
        _packages.Add(package);
        PackagesChanged?.Invoke();
    }

    public void RemovePackage(Package package)
    {
        // Check if this package exists in the custom packages list
        var existingPackage = _packages.FirstOrDefault(p => ReferenceEquals(p, package));
        if (existingPackage != null)
        {
            _packages.Remove(existingPackage);
            // Clean up tracking
            _originalTitles.Remove(existingPackage);
        }
        else
        {
            // If not in custom packages list, check if we're tracking its original title
            var originalTitle = _originalTitles.TryGetValue(package, out var title) 
                ? title 
                : package.Title;
            
            _deletedPackageIds.Add(originalTitle);
            
            // Clean up tracking
            _originalTitles.Remove(package);
        }
        
        PackagesChanged?.Invoke();
    }

    public void UpdatePackage(Package oldPackage, Package newPackage)
    {
        // Check if this is a default package being updated for the first time
        var isDefaultPackage = GetDefaultPackages().Any(p => p.Title == oldPackage.Title);
        
        if (isDefaultPackage && !_originalTitles.ContainsKey(oldPackage))
        {
            // Track the original title before first modification
            _originalTitles[oldPackage] = oldPackage.Title;
        }
        
        var existingPackage = _packages.FirstOrDefault(p => ReferenceEquals(p, oldPackage));
        if (existingPackage != null)
        {
            // Update existing custom package
            var index = _packages.IndexOf(existingPackage);
            _packages[index] = newPackage;
            
            // Transfer original title tracking if it exists
            if (_originalTitles.Remove(oldPackage, out var originalTitle))
            {
                _originalTitles[newPackage] = originalTitle;
            }
        }
        else
        {
            // This is a default package being modified
            _packages.Add(newPackage);
            
            // Mark original as deleted
            var originalTitle = _originalTitles.TryGetValue(oldPackage, out var title) 
                ? title 
                : oldPackage.Title;
            
            _deletedPackageIds.Add(originalTitle);
            
            // Track the new package with its original title
            _originalTitles[newPackage] = originalTitle;
            
            // Clean up old tracking
            _originalTitles.Remove(oldPackage);
        }
        PackagesChanged?.Invoke();
    }
}