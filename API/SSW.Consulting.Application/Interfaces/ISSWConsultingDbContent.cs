﻿using Microsoft.EntityFrameworkCore;
using SSW.Consulting.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace SSW.Consulting.Application.Interfaces
{
    public interface ISSWConsultingDbContext
    {
        DbSet<StaffMember> StaffMembers { get; set; }
        DbSet<StaffMemberSkill> StaffMemberSkills { get; set; }
        DbSet<Skill> Skills { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
