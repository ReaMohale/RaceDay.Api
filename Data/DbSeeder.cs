using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Data;

/// <summary>
/// Demo data for Development: two organisers, five participants, events (dates are relative to today so
/// the home page always has upcoming events), categories, enrolments and published results.
/// Does nothing when the database already contains users.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Passw0rd!123";

    public static void Seed(RaceDayDbContext db, IPasswordService passwords)
    {
        if (db.Organisers.Any() || db.Participants.Any())
            return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        Organiser NewOrganiser(string name, string email, string phone) =>
            new() { FullName = name, Email = email, PhoneNumber = phone, PasswordHash = passwords.Hash(DemoPassword) };
        Participant NewParticipant(string name, string email, string phone) =>
            new() { FullName = name, Email = email, PhoneNumber = phone, PasswordHash = passwords.Hash(DemoPassword) };

        var thandiwe = NewOrganiser("Thandiwe Nkosi", "thandiwe.nkosi@raceday.co.za", "0821234567");
        var johan = NewOrganiser("Johan van der Merwe", "johan.vdm@raceday.co.za", "0837654321");

        var lerato = NewParticipant("Lerato Mokoena", "lerato.mokoena@gmail.com", "0731112222");
        var sipho = NewParticipant("Sipho Dlamini", "sipho.dlamini@gmail.com", "0743334444");
        var nomsa = NewParticipant("Nomsa Khumalo", "nomsa.khumalo@gmail.com", "0725556666");
        var pieter = NewParticipant("Pieter Botha", "pieter.botha@gmail.com", "0767778888");
        var aisha = NewParticipant("Aisha Patel", "aisha.patel@gmail.com", "0829990000");

        Event NewEvent(Organiser owner, string name, string description, int daysFromToday, string location, string type, decimal km) =>
            new()
            {
                Organiser = owner,
                EventName = name,
                Description = description,
                Location = location,
                EventType = type,
                DistanceKm = km,
                EventDate = today.AddDays(daysFromToday)
            };

        Category AddCategory(Event ev, string name, decimal km, decimal fee, int max)
        {
            var category = new Category { Event = ev, CategoryName = name, DistanceKm = km, EntryFee = fee, MaxParticipants = max };
            ev.Categories.Add(category);
            return category;
        }

        Enrolment Enrol(Participant who, Category category, TimeSpan? finish = null, int? position = null)
        {
            var enrolment = new Enrolment { Participant = who, Event = category.Event, Category = category, Status = "Confirmed" };
            if (finish is not null)
                enrolment.Result = new Result { FinishTime = finish.Value, Position = position };
            db.Enrolments.Add(enrolment);
            return enrolment;
        }

        var riverRun = NewEvent(thandiwe, "Standerton River Run", "A scenic riverside road running event for all levels.",
            21, "Standerton, Mpumalanga", "Run", 21.1m);
        var riverTen = AddCategory(riverRun, "10km Run", 10m, 150m, 300);
        var riverHalf = AddCategory(riverRun, "21km Half Marathon", 21.1m, 250m, 200);

        var charityCycle = NewEvent(thandiwe, "Highveld Charity Cycle", "Community cycling event raising funds for local schools.",
            -45, "Standerton, Mpumalanga", "Cycle", 80m);
        var cycle40 = AddCategory(charityCycle, "40km Road Cycle", 40m, 200m, 150);
        AddCategory(charityCycle, "80km Road Cycle", 80m, 300m, 100);

        var parkRun = NewEvent(johan, "Johannesburg City Park Run", "A family-friendly park run through the city centre.",
            35, "Johannesburg, Gauteng", "Run", 10m);
        var park5 = AddCategory(parkRun, "5km Fun Run", 5m, 50m, 500);
        AddCategory(parkRun, "10km Run", 10m, 100m, 300);

        var heritageWalk = NewEvent(johan, "Soweto Heritage Walk", "A guided walk past the landmarks of Soweto.",
            14, "Soweto, Gauteng", "Walk", 10m);
        var walk5 = AddCategory(heritageWalk, "5km Family Walk", 5m, 0m, 400);
        AddCategory(heritageWalk, "10km Heritage Walk", 10m, 80m, 200);

        var spring10k = NewEvent(johan, "Pretoria Spring 10K", "A flat, fast 10 km through the jacaranda streets.",
            -20, "Pretoria, Gauteng", "Run", 10m);
        var spring10 = AddCategory(spring10k, "10km Open", 10m, 120m, 250);

        db.Organisers.AddRange(thandiwe, johan);
        db.Participants.AddRange(lerato, sipho, nomsa, pieter, aisha);
        db.Events.AddRange(riverRun, charityCycle, parkRun, heritageWalk, spring10k);

        // Past events with published results (so "3rd out of 4 finishers" can be shown).
        Enrol(pieter, cycle40, new TimeSpan(1, 8, 44), 1);
        Enrol(sipho, cycle40, new TimeSpan(1, 12, 7), 2);
        Enrol(aisha, cycle40, new TimeSpan(1, 21, 30), 3);

        Enrol(nomsa, spring10, new TimeSpan(0, 47, 31), 1);
        Enrol(pieter, spring10, new TimeSpan(0, 49, 2), 2);
        Enrol(lerato, spring10, new TimeSpan(0, 52, 18), 3);
        Enrol(aisha, spring10, new TimeSpan(0, 58, 40), 4);

        // Upcoming events with entries and no results yet.
        Enrol(lerato, riverTen);
        Enrol(sipho, riverHalf);
        Enrol(lerato, walk5);
        Enrol(nomsa, park5);

        db.SaveChanges();
    }
}
