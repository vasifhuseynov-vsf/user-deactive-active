﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentCar.DAL;
using RentCar.Models.Entities;
using RentCar.Models.ViewModels;
using RentCar.Models.ViewModels.Car;
using RentCar.Tools.EmailHandler;
using RentCar.Tools.EmailHandler.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RentCar.Controllers
{
    public class CarController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _mailService;

        public CarController(AppDbContext dbContext, UserManager<IdentityUser> userManager, IEmailService mailService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _mailService = mailService;
        }

        public async Task<IActionResult> Index()
        {
            var cars = await _dbContext.CarModels.Where(c => !c.IsDeleted)
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => i.IsMain)).ToListAsync();

            return View(cars);
        }

        //***** Detail *****//
        public async Task<IActionResult> Detail(int id)
        {
            var car = await _dbContext.CarModels
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == id);
            var comments = await _dbContext.Comments.Where(c => c.CarModelId == id).ToListAsync();

            var imageNames = await _dbContext.CarImages.Where(i => i.CarModelId == id && !i.IsDeleted).Select(i => i.ImageName).ToListAsync();

            string[] imageNamesArr = imageNames.ToArray();

            return View(new CarDetailViewModel
            {
                CarModel = car,
                Comments = comments,
                ImageNames = imageNamesArr
            });
        }

        //***** Comment *****//
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(CreateCommentViewModel model)
        {
            IdentityUser user = await _userManager.GetUserAsync(User);

            Comment comment = new Comment
            {
                UserName = user.UserName,
                CarModelId = model.CarModelId,
                Text = model.Text
            };

            await _dbContext.Comments.AddAsync(comment);
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Detail), "Car", new { id = model.CarModelId });
        }

        //***** Renting *****//
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renting(RentingViewModel model)
        {
            IdentityUser user = await _userManager.GetUserAsync(User);

            if (model.StartDate > model.EndDate || model.StartDate < DateTime.Now || model.EndDate < DateTime.Now)
            {
                TempData["warning"] = "<script>alert('Tarix düzgün göstərilməyib');</script>";
            }
            else
            {
                RentedCar rentedCar = new RentedCar
                {
                    CarModelId = model.CarModelId,
                    UserId = user.Id,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate
                };
                await _dbContext.RentedCars.AddAsync(rentedCar);
                await _dbContext.SaveChangesAsync();

                await _mailService.SendEMailAsync(new MailRequest
                {
                    ToEmail = user.Email,
                    Subject = "Qeyd",
                    Body = $"Avtomobil uğurla kirayələndi. Başlanğıç tarix: {model.StartDate.ToString("dd/MM/yyyy")}. " +
                    $"Son tarix: {model.EndDate.ToString("dd/MM/yyyy")}"
                });

                TempData["msg"] = "<script>alert('Avtomobil uğurla kirayələndi');</script>";

            }

            return RedirectToAction(nameof(Detail), nameof(Car), new { id = model.CarModelId });
        }


    }
}