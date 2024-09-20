﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyReplayLibrary.Data;

#nullable disable

namespace MyReplayLibrary.Data.Migrations
{
    [DbContext(typeof(ReplayDbContext))]
    [Migration("20240921125903_Takedowns")]
    partial class Takedowns
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseCollation("NOCASE")
                .HasAnnotation("ProductVersion", "8.0.3");

            modelBuilder.Entity("MyReplayLibrary.Data.Models.BuildNumber", b =>
                {
                    b.Property<int>("Buildnumber1")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("Builddate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Version")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.HasKey("Buildnumber1");

                    b.ToTable("BuildNumbers");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.HeroTalentInformation", b =>
                {
                    b.Property<string>("Character")
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.Property<int>("ReplayBuildFirst")
                        .HasColumnType("INTEGER");

                    b.Property<int>("TalentId")
                        .HasMaxLength(450)
                        .HasColumnType("INTEGER");

                    b.Property<int>("ReplayBuildLast")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TalentDescription")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<string>("TalentName")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.Property<int>("TalentTier")
                        .HasColumnType("INTEGER");

                    b.HasKey("Character", "ReplayBuildFirst", "TalentId");

                    b.ToTable("HeroTalentInformations");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.PlayerEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("BattleNetId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BattleNetRegionId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BattleNetSubId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BattleTag")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset>("TimestampCreated")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacter", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("CharacterId")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.Property<int>("CharacterLevel")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsAutoSelect")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsMe")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsWinner")
                        .HasColumnType("INTEGER");

                    b.HasKey("ReplayId", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayCharacters");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterDraftOrder", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("DraftOrder")
                        .HasColumnType("INTEGER");

                    b.HasKey("ReplayId", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayCharacterDraftOrders");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterMatchAward", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MatchAwardType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ReplayId", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayCharacterMatchAwards");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterScoreResult", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Assists")
                        .HasColumnType("INTEGER");

                    b.Property<int>("CreepDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("DamageTaken")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Deaths")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ExperienceContribution")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Healing")
                        .HasColumnType("INTEGER");

                    b.Property<int>("HeroDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("HighestKillStreak")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Level")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MercCampCaptures")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MetaExperience")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MinionDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("SelfHealing")
                        .HasColumnType("INTEGER");

                    b.Property<int>("SiegeDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("SoloKills")
                        .HasColumnType("INTEGER");

                    b.Property<int>("StructureDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("SummonDamage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Takedowns")
                        .HasColumnType("INTEGER");

                    b.Property<TimeSpan>("TimeSpentDead")
                        .HasColumnType("TEXT");

                    b.Property<int>("TownKills")
                        .HasColumnType("INTEGER");

                    b.Property<int>("WatchTowerCaptures")
                        .HasColumnType("INTEGER");

                    b.HasKey("ReplayId", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayCharacterScoreResults");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterTalent", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("TalentId")
                        .HasColumnType("INTEGER");

                    b.HasKey("ReplayId", "PlayerId", "TalentId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayCharacterTalents");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("GameMode")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("MapId")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("TEXT");

                    b.Property<int>("ReplayBuild")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("ReplayHash")
                        .HasColumnType("TEXT");

                    b.Property<TimeSpan>("ReplayLength")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("TimestampCreated")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("TimestampReplay")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Replays");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayTeamObjective", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsWinner")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TeamObjectiveType")
                        .HasColumnType("TEXT");

                    b.Property<TimeSpan>("TimeSpan")
                        .HasColumnType("TEXT");

                    b.Property<int?>("PlayerId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Value")
                        .HasColumnType("INTEGER");

                    b.HasKey("ReplayId", "IsWinner", "TeamObjectiveType", "TimeSpan");

                    b.HasIndex("PlayerId");

                    b.ToTable("ReplayTeamObjectives");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.Takedown", b =>
                {
                    b.Property<int>("ReplayId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("SeqId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("VictimId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("KillerId")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("KillingBlow")
                        .HasColumnType("INTEGER");

                    b.Property<TimeSpan>("TimeSpan")
                        .HasColumnType("TEXT");

                    b.HasKey("ReplayId", "SeqId", "VictimId", "KillerId");

                    b.HasIndex("ReplayId", "KillerId");

                    b.HasIndex("ReplayId", "VictimId");

                    b.ToTable("Takedowns");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacter", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany("ReplayCharacters")
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany("ReplayCharacters")
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterDraftOrder", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany()
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "ReplayCharacter")
                        .WithOne("ReplayCharacterDraftOrder")
                        .HasForeignKey("MyReplayLibrary.Data.Models.ReplayCharacterDraftOrder", "ReplayId", "PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");

                    b.Navigation("ReplayCharacter");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterMatchAward", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany()
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "ReplayCharacter")
                        .WithMany("ReplayCharacterMatchAwards")
                        .HasForeignKey("ReplayId", "PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");

                    b.Navigation("ReplayCharacter");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterScoreResult", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany()
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "ReplayCharacter")
                        .WithOne("ReplayCharacterScoreResult")
                        .HasForeignKey("MyReplayLibrary.Data.Models.ReplayCharacterScoreResult", "ReplayId", "PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");

                    b.Navigation("ReplayCharacter");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacterTalent", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany()
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "ReplayCharacter")
                        .WithMany("ReplayCharacterTalents")
                        .HasForeignKey("ReplayId", "PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");

                    b.Navigation("ReplayCharacter");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayTeamObjective", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.PlayerEntry", "Player")
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany("ReplayTeamObjectives")
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");

                    b.Navigation("Replay");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.Takedown", b =>
                {
                    b.HasOne("MyReplayLibrary.Data.Models.ReplayEntry", "Replay")
                        .WithMany()
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "Killer")
                        .WithMany()
                        .HasForeignKey("ReplayId", "KillerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyReplayLibrary.Data.Models.ReplayCharacter", "Victim")
                        .WithMany()
                        .HasForeignKey("ReplayId", "VictimId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Killer");

                    b.Navigation("Replay");

                    b.Navigation("Victim");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.PlayerEntry", b =>
                {
                    b.Navigation("ReplayCharacters");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayCharacter", b =>
                {
                    b.Navigation("ReplayCharacterDraftOrder");

                    b.Navigation("ReplayCharacterMatchAwards");

                    b.Navigation("ReplayCharacterScoreResult")
                        .IsRequired();

                    b.Navigation("ReplayCharacterTalents");
                });

            modelBuilder.Entity("MyReplayLibrary.Data.Models.ReplayEntry", b =>
                {
                    b.Navigation("ReplayCharacters");

                    b.Navigation("ReplayTeamObjectives");
                });
#pragma warning restore 612, 618
        }
    }
}
