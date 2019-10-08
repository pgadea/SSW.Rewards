﻿using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using MoreLinq;
using SSW.Consulting.Application.Common.Interfaces;
using SSW.Consulting.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SSW.Consulting.Persistence
{
    public class SampleDataSeeder
    {
        private readonly ISSWConsultingDbContext _context;

        private Dictionary<string, int> _skills;
        private Dictionary<string, StaffMember> _staffMembers;

        public SampleDataSeeder(ISSWConsultingDbContext context)
        {
            _context = context;
        }

        public async Task SeedAllAsync(byte[] profileData, CancellationToken cancellationToken)
        {
            var profiles = GetProfiles(profileData)
                .Where(p => !string.IsNullOrWhiteSpace(p.Profile))
                .ToList();

            await SeedSkillsAsync(profiles.SelectMany(p => p.Skills).Distinct(), cancellationToken);
            await SeedStaffMembers(profiles, cancellationToken);
            //await SeedAchievementsAsync(cancellationToken);
        }

        private async Task SeedSkillsAsync(IEnumerable<string> newSkills, CancellationToken cancellationToken)
        {
            var existingSkills = await _context.Skills.ToListAsync(cancellationToken);
            _context.Skills.RemoveRange(existingSkills.Where(s => !newSkills.Contains(s.Name)));
            await _context.Skills.AddRangeAsync(newSkills.Where(s => !existingSkills.Any(es => es.Name == s)).Select(s => new Skill { Name = s }), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _skills = await _context.Skills.ToDictionaryAsync(s => s.Name, s => s.Id);
        }

        private async Task SeedStaffMembers(IEnumerable<UserProfile> profiles, CancellationToken cancellationToken)
        {
            //nuke all skills (will be re-added later)
            _context.StaffMemberSkills.RemoveRange(await _context.StaffMemberSkills.ToArrayAsync(cancellationToken));
            await _context.SaveChangesAsync(cancellationToken);

            //remove removed profiles
            var profileNames = profiles.Select(p => p.Name).ToArray();
            var profilesToRemove = await _context
                .StaffMembers
                .Where(sm => !profileNames.Contains(sm.Name))
                .ToArrayAsync(cancellationToken);
            _context.StaffMembers.RemoveRange(profilesToRemove);
            await _context.SaveChangesAsync(cancellationToken);

            //add/update profiles
            var existingStaffMembers = await _context.StaffMembers.ToListAsync(cancellationToken);
            var existingAcheivements = await _context.Achievements.ToListAsync(cancellationToken);
            profiles.ForEach(p =>
            {
                var staffMember = existingStaffMembers.FirstOrDefault(sm => sm.Name == p.Name) ?? new StaffMember();
                staffMember.Name = p.Name;
                staffMember.Title = p.Title;
                staffMember.StaffMemberSkills = p.Skills.Select(s => new StaffMemberSkill { SkillId = _skills[s] }).ToArray();
                staffMember.Profile = p.Profile;

                if(staffMember.Id == 0)
                {
                    _context.StaffMembers.Add(staffMember);
                }

                var achievement = existingAcheivements.FirstOrDefault(a => a.Name.Contains(p.Name)) ?? new Achievement();
                achievement.Name = staffMember.Name;
                achievement.Value = p.Value;

                if (achievement.Id == 0)
                {
                    _context.Achievements.Add(achievement);
                }
            });

            await _context.SaveChangesAsync(cancellationToken);
            _staffMembers = await _context.StaffMembers.ToDictionaryAsync(s => s.Name, s => s);
        }

        private IEnumerable<UserProfile> GetProfiles(byte[] profileData)
        {
            using (var stream = new MemoryStream(profileData))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                reader.Read();
                while (reader.Read())
                {
                    yield return new UserProfile
                    {
                        Name = reader.GetString(0)?.Trim(),
                        Title = reader.GetString(1)?.Trim(),
                        Skills = Enumerable.Range(2, 4)
                            .Select(i => reader.GetString(i)?.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray(),
                        Profile = reader.GetString(6)?.Trim(),
                        Value = (int)reader.GetDouble(7)
                    };
                }
            }
        }

        private class UserProfile
        {
            public string Name { get; set; }
            public string Title { get; set; }
            public string[] Skills { get; set; }
            public string Profile { get; set; }
            public int Value { get; set; }
        }
    }
}