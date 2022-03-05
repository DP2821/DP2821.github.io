﻿using Helperland.Data;
using Helperland.Functionality;
using Helperland.GlobalVariable;
using Helperland.Models;
using Helperland.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Security.Cryptography;

namespace Helperland.Controllers
{
    public class CustomerController : Controller
    {
        HelperlandContext _helperlandContext;
        MailRequest mailRequest = new MailRequest();
        MD5 md5Hash = MD5.Create();


        public CustomerController(HelperlandContext helperlandContext)
        {
            _helperlandContext = helperlandContext;
        }
        
        public IActionResult BookService()
        {
            int UsertypeId = new CurrentLoggedInUser().GetUserTypeId(Request.Cookies["keepMeLoggedInToken"]);
            if (UsertypeId != -1)
            {
                if (UsertypeId == new GlobalData().CustomerTypeId)
                {
                    return View();
                }
                else
                {
                    //If user is logged in but not customer than simply redirect to Home.
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                //If user is not logged in than, will redirect to Home and open login modal
                return RedirectToAction("Index", "Home", new { loginModal = "true" });
            }
        }

        public IActionResult ServiceHistory()
        {

            int? UsertypeId = new CurrentLoggedInUser().GetUserTypeId(Request.Cookies["keepMeLoggedInToken"]);
            if (UsertypeId != null)
            {
                if (UsertypeId == new GlobalData().CustomerTypeId)
                {
                    return View();
                }
                else
                {
                    //If user is logged in but not customer than simply redirect to Home.
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                //If user is not logged in than, will redirect to Home and open login modal
                return RedirectToAction("Index", "Home", new { loginModal = "true" });
            }
        }

        public string IsValidPostalCode(string PostalCode)
        {

            int count = _helperlandContext.Users.Where(u => u.ZipCode == PostalCode && u.UserTypeId == new GlobalData().SpTypeId).Select(u => u.ZipCode).Count();

            if (count != 0)
            {
                var x = (from z in _helperlandContext.Zipcodes
                 join c in _helperlandContext.Cities on z.CityId equals c.Id
                 where z.ZipcodeValue == PostalCode
                 select new {
                     CityName = c.CityName
                 }).FirstOrDefault();
                return JsonSerializer.Serialize(x);
            }
            else
            {
                return "false";
            }
        }

        public string GetUserAddresses(string postalCode)
        {

            int? userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            return JsonSerializer.Serialize(_helperlandContext.UserAddresses.Where(u => u.UserId == userId && u.PostalCode == postalCode && u.IsDeleted == false).ToList());
        }

        public string SaveUserAddress(NewAddressBookServiceViewModel newAddressBookServiceViewModel)
        {
            UserAddress userAddress = new UserAddress();
            userAddress.UserId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            userAddress.AddressLine1 = newAddressBookServiceViewModel.StreetName;
            userAddress.AddressLine2 = newAddressBookServiceViewModel.HouseNumber;
            userAddress.PostalCode = newAddressBookServiceViewModel.PostalCode;
            userAddress.City = newAddressBookServiceViewModel.City;
            userAddress.Mobile = newAddressBookServiceViewModel.Phone;
            userAddress.Email = new CurrentLoggedInUser().GetEmail(Request.Cookies["keepMeLoggedInToken"]);
            _helperlandContext.UserAddresses.Add(userAddress);
            int x = _helperlandContext.SaveChanges();

            return "" + x;
        }

        public string GetFevoriteServiceProviders(string data)
        {
            int? userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            var fevSPListBookServiceViewModel = (from f in _helperlandContext.FavoriteAndBlockeds
                                                 join u in _helperlandContext.Users
                                                 on f.TargetUserId equals u.UserId
                                                 where f.UserId == userId && f.IsFavorite == true && f.IsBlocked == false
                                                 select new
                                                 {
                                                     TargetUserID = u.UserId,
                                                     TargetUserName = u.FirstName + " " + u.LastName
                                                 }).ToList();

            return JsonSerializer.Serialize(fevSPListBookServiceViewModel);
        }

        public string CompleteBooking(CompleteBookingViewModel completeBookingViewModel)
        {
            int serviceRequestId;
            if (ModelState.IsValid)
            {
                int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);
                string userName = new CurrentLoggedInUser().GetName(Request.Cookies["keepMeLoggedInToken"]);

                _helperlandContext.ServiceRequests.Add(new CompleteBookingRepository().GetServiceRequest(completeBookingViewModel, userId));
                int noOfRowsChanged = _helperlandContext.SaveChanges();

                if (noOfRowsChanged >= 1)
                {
                    serviceRequestId = _helperlandContext.ServiceRequests.Where(u => u.UserId == userId).Max(u => u.ServiceRequestId);

                    if (completeBookingViewModel.ExtraHoursList != null)
                    {
                        for (int i = 0; i < completeBookingViewModel.ExtraHoursList.Length; i++)
                        {
                            ServiceRequestExtra serviceRequestExtra = new ServiceRequestExtra();
                            serviceRequestExtra.ServiceRequestId = serviceRequestId;
                            serviceRequestExtra.ServiceExtraId = completeBookingViewModel.ExtraHoursList[i];

                            _helperlandContext.ServiceRequestExtras.Add(serviceRequestExtra);
                        }
                        _helperlandContext.SaveChanges();
                    }

                    UserAddress? userAddress = _helperlandContext.UserAddresses.Where(u => u.AddressId == completeBookingViewModel.AddressId).FirstOrDefault();

                    if (userAddress != null)
                    {
                        ServiceRequestAddress serviceRequestAddress = new ServiceRequestAddress();
                        serviceRequestAddress.ServiceRequestId = serviceRequestId;
                        serviceRequestAddress.AddressLine1 = userAddress.AddressLine1;
                        serviceRequestAddress.AddressLine2 = userAddress.AddressLine2;
                        serviceRequestAddress.City = userAddress.City;
                        serviceRequestAddress.Mobile = userAddress.Mobile;
                        serviceRequestAddress.PostalCode = userAddress.PostalCode;
                        _helperlandContext.ServiceRequestAddresses.Add(serviceRequestAddress);
                        _helperlandContext.SaveChanges();
                    }
                    else
                    {
                        return "false";
                    }

                    /*
                        Sending Email to service provider
                    */

                    if (completeBookingViewModel.FevServiceProviderID != null)
                    {

                        var fevSP = _helperlandContext.Users.Where(u => u.UserId == completeBookingViewModel.FevServiceProviderID).Select(u => new { u.Email, u.FirstName, u.LastName }).FirstOrDefault();

                        mailRequest.SendEmail(fevSP.Email, fevSP.FirstName + " " + fevSP.LastName, "New Service Request",
                        "Hello,\n" +
                        fevSP.FirstName + " " + fevSP.LastName + "\n\n" +
                        userName + " has booked service at:\n" +
                        userAddress.AddressLine1 + ", " + userAddress.AddressLine2 + "\n" +
                        userAddress.City + "-" + userAddress.PostalCode + "\n" +
                        "Phone: " + userAddress.Mobile);
                    }
                    else
                    {
                        var blockList = _helperlandContext.FavoriteAndBlockeds.Where(u => u.UserId == userId && u.IsBlocked == true).Select(u => u.TargetUserId).ToList();

                        var spList = _helperlandContext.Users.Where(u => u.ZipCode == userAddress.PostalCode && u.UserTypeId == new GlobalData().SpTypeId).Select(u => new { u.UserId, u.Email, u.FirstName, u.LastName }).ToList();

                        for (int i = 0; i < spList.Count; i++)
                        {
                            var fevSP = spList[i];
                            if(!blockList.Contains(fevSP.UserId)){
                                mailRequest.SendEmail(fevSP.Email, fevSP.FirstName + " " + fevSP.LastName, "New Service Request",
                                "Hello,\n" +
                                fevSP.FirstName + " " + fevSP.LastName + "\n\n" +
                                userName + " has booked service at:\n" +
                                userAddress.AddressLine1 + ", " + userAddress.AddressLine2 + "\n" +
                                userAddress.City + "-" + userAddress.PostalCode + "\n" +
                                "Phone: " + userAddress.Mobile);
                            }
                        }
                    }

                }
                else
                {
                    return "false";
                }
            }
            else
            {
                return "false";
            }

            return "" + serviceRequestId;
        }

        public string GetNewServices(string modal)
        {
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            var serviceRequests = (from sr in _helperlandContext.ServiceRequests
                                   join sa in _helperlandContext.ServiceRequestAddresses on sr.ServiceRequestId equals sa.ServiceRequestId
                                   // join se in _helperlandContext.ServiceRequestExtras on sr.ServiceRequestId equals se.ServiceRequestId
                                   where sr.UserId == userId && sr.Status == new GlobalData().SERVICE_REQUEST_STATUS_NEW
                                   select new
                                   {
                                       ServiceId = sr.ServiceRequestId,
                                       ServiceStartDate = sr.ServiceStartDate.ToString("d"),
                                       ServiceStartTime = sr.ServiceStartDate.ToString("HH:mm"),
                                       ServiceTotalHour = sr.ServiceHours + sr.ExtraHours,
                                       TotalCost = sr.TotalCost,
                                       Comments = sr.Comments,
                                       ServiceProviderId = sr.ServiceProviderId,
                                       ServiceProviderFirstName = _helperlandContext.Users.Where(u => u.UserId == sr.ServiceProviderId).Select(u => u.FirstName).FirstOrDefault(),
                                       ServiceProviderLastName = _helperlandContext.Users.Where(u => u.UserId == sr.ServiceProviderId).Select(u => u.LastName).FirstOrDefault(),
                                       HasPets = sr.HasPets,

                                       AverageRatings = _helperlandContext.Ratings.Where(u => u.RatingTo == sr.ServiceProviderId).Select(u => u.Ratings).ToList(),

                                       AddressLine1 = sa.AddressLine1,
                                       AddressLine2 = sa.AddressLine2,
                                       City = sa.City,
                                       PostalCode = sa.PostalCode,
                                       Mobile = sa.Mobile,
                                       Email = sa.Email,

                                       // ServiceExtraId = se.ServiceExtraId
                                       ServiceExtraId = _helperlandContext.ServiceRequestExtras.Where(u => u.ServiceRequestId == sr.ServiceRequestId).Select(u => u.ServiceExtraId).ToList()

                                   }).ToList();


            return JsonSerializer.Serialize(serviceRequests);
        }

        public string GetCompletedCancelledServices(string modal)
        {
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            var serviceRequests = (from sr in _helperlandContext.ServiceRequests
                                   join sa in _helperlandContext.ServiceRequestAddresses on sr.ServiceRequestId equals sa.ServiceRequestId
                                   // join se in _helperlandContext.ServiceRequestExtras on sr.ServiceRequestId equals se.ServiceRequestId
                                   where sr.UserId == userId && sr.Status != new GlobalData().SERVICE_REQUEST_STATUS_NEW
                                   select new
                                   {
                                       ServiceId = sr.ServiceRequestId,
                                       ServiceStartDate = sr.ServiceStartDate.ToString("d"),
                                       ServiceStartTime = sr.ServiceStartDate.ToString("HH:mm"),
                                       ServiceTotalHour = sr.ServiceHours + sr.ExtraHours,
                                       TotalCost = sr.TotalCost,
                                       Comments = sr.Comments,
                                       ServiceProviderId = sr.ServiceProviderId,
                                       ServiceProviderFirstName = _helperlandContext.Users.Where(u => u.UserId == sr.ServiceProviderId).Select(u => u.FirstName).FirstOrDefault(),
                                       ServiceProviderLastName = _helperlandContext.Users.Where(u => u.UserId == sr.ServiceProviderId).Select(u => u.LastName).FirstOrDefault(),
                                       HasPets = sr.HasPets,
                                       Status = sr.Status,

                                       AverageRatings = _helperlandContext.Ratings.Where(u => u.RatingTo == sr.ServiceProviderId).Select(u => u.Ratings).ToList(),

                                       AddressLine1 = sa.AddressLine1,
                                       AddressLine2 = sa.AddressLine2,
                                       City = sa.City,
                                       PostalCode = sa.PostalCode,
                                       Mobile = sa.Mobile,
                                       Email = sa.Email,

                                       // ServiceExtraId = se.ServiceExtraId
                                       ServiceExtraId = _helperlandContext.ServiceRequestExtras.Where(u => u.ServiceRequestId == sr.ServiceRequestId).Select(u => u.ServiceExtraId).ToList()

                                   }).ToList();


            return JsonSerializer.Serialize(serviceRequests);
        }


        public string RescheduleService(RescheduleServiceViewModel rescheduleServiceViewModel)
        {

            if (ModelState.IsValid)
            {
                ServiceRequest? serviceRequest = _helperlandContext.ServiceRequests.Find(rescheduleServiceViewModel.ServiceId);

                if (serviceRequest != null)
                {

                    if (serviceRequest.ServiceProviderId != null)
                    {

                        // int noOfServiceConflicting = _helperlandContext.ServiceRequests.Where(u => u.ServiceProviderId == serviceRequest.ServiceProviderId &&
                        //         (
                        //             u.ServiceStartDate.Date == DateTime.ParseExact(rescheduleServiceViewModel.NewServiceDate, "yyyy-MM-dd", null).Date &&
                        //             (
                        //                 u.ServiceStartDate.Hour >
                        //                 (
                        //                     DateTime.ParseExact(rescheduleServiceViewModel.NewServicetime, "HH:mm", null).Hour +
                        //                     DateTime.ParseExact(Math.Round(serviceRequest.ServiceHours) + "", "HH", null).Hour +
                        //                     DateTime.ParseExact(Math.Round(serviceRequest.ExtraHours == null ? (double)serviceRequest.ExtraHours : 0.0) + "", "HH", null).Hour
                        //                 ) ||
                        //                 DateTime.ParseExact(rescheduleServiceViewModel.NewServicetime, "HH:mm", null).Hour >
                        //                 (
                        //                     u.ServiceStartDate.Hour +
                        //                     DateTime.ParseExact(Math.Round(u.ServiceHours) + "", "HH", null).Hour +
                        //                     DateTime.ParseExact(Math.Round(u.ExtraHours == null ? (double)u.ExtraHours : 0.0) + "", "HH", null).Hour
                        //                 )
                        //             )
                        //         )
                        //     ).Count();
                        var sameDateServices = _helperlandContext.ServiceRequests.Where(u => u.ServiceProviderId == serviceRequest.ServiceProviderId && u.Status == new GlobalData().SERVICE_REQUEST_STATUS_NEW && u.ServiceRequestId != serviceRequest.ServiceRequestId &&
                                    u.ServiceStartDate.Date == DateTime.ParseExact(rescheduleServiceViewModel.NewServiceDate, "yyyy-MM-dd", null).Date
                            ).Select(u => new { u.ServiceStartDate, u.ServiceHours, u.ExtraHours }).ToList();

                        for (int i = 0; i < sameDateServices.Count; i++)
                        {
                            if (!(sameDateServices[i].ServiceStartDate.Hour >
                                        DateTime.ParseExact(rescheduleServiceViewModel.NewServicetime, "HH:mm", null).AddHours(serviceRequest.ServiceHours + (double)serviceRequest.ExtraHours).Hour
                                ||
                                DateTime.ParseExact(rescheduleServiceViewModel.NewServicetime, "HH:mm", null).Hour >
                                        sameDateServices[i].ServiceStartDate.AddHours(sameDateServices[i].ServiceHours + (double)sameDateServices[i].ExtraHours).Hour
                                ))
                            {
                                string startMinute = sameDateServices[i].ServiceStartDate.Minute + "";
                                string endMinute = sameDateServices[i].ServiceStartDate.AddHours(sameDateServices[i].ServiceHours + (double)sameDateServices[i].ExtraHours).Minute +"";
                                if(sameDateServices[i].ServiceStartDate.Minute == 0){
                                    startMinute = "00";
                                }
                                if(sameDateServices[i].ServiceStartDate.AddHours(sameDateServices[i].ServiceHours + (double)sameDateServices[i].ExtraHours).Minute == 0){
                                    endMinute = "00";
                                }
                                return $"Another service request has been assigned to the service provider on {sameDateServices[i].ServiceStartDate.Day}-{sameDateServices[i].ServiceStartDate.Month}-{sameDateServices[i].ServiceStartDate.Year} from {sameDateServices[i].ServiceStartDate.Hour}:{startMinute} to {sameDateServices[i].ServiceStartDate.AddHours(sameDateServices[i].ServiceHours + (double)sameDateServices[i].ExtraHours).Hour}:{endMinute}. Either choose another date or pick up a different time slot";
                            }
                        }

                        string userName = new CurrentLoggedInUser().GetName(Request.Cookies["keepMeLoggedInToken"]);

                        var fevSP = _helperlandContext.Users.Where(u => u.UserId == serviceRequest.ServiceProviderId).Select(u => new { u.Email, u.FirstName, u.LastName }).FirstOrDefault();

                        mailRequest.SendEmail(fevSP.Email, fevSP.FirstName + " " + fevSP.LastName, "Service Rescheduled",
                            "Hello,\n" +
                            fevSP.FirstName + " " + fevSP.LastName + "\n\n" +
                            userName + " has rescheduled service on\n" +
                            rescheduleServiceViewModel.NewServiceDate + " " + rescheduleServiceViewModel.NewServicetime + "\n" +
                            "Service ID: " + rescheduleServiceViewModel.ServiceId
                        );
                    }

                    serviceRequest.ServiceStartDate = DateTime.ParseExact(rescheduleServiceViewModel.NewServiceDate + " " + rescheduleServiceViewModel.NewServicetime, "yyyy-MM-dd HH:mm", null);
                    serviceRequest.ModifiedDate = DateTime.Now;
                    _helperlandContext.ServiceRequests.Update(serviceRequest);
                    _helperlandContext.SaveChanges();
                }
                else
                {
                    return "No service found by that id";
                }
            }
            else
            {
                return "Something is missing";
            }
            return "true";
        }

        public string CancelService(CancelServiceViewModel cancelServiceViewModel)
        {

            if (ModelState.IsValid)
            {
                ServiceRequest? serviceRequest = _helperlandContext.ServiceRequests.Find(cancelServiceViewModel.ServiceId);
                if (serviceRequest != null)
                {
                    if (serviceRequest.ServiceProviderId != null)
                    {
                        string userName = new CurrentLoggedInUser().GetName(Request.Cookies["keepMeLoggedInToken"]);

                        var serviceProvider = _helperlandContext.Users.Where(u => u.UserId == serviceRequest.ServiceProviderId).Select(u => new { u.Email, u.FirstName, u.LastName }).FirstOrDefault();

                        mailRequest.SendEmail(serviceProvider.Email, serviceProvider.FirstName + " " + serviceProvider.LastName, "Service Cancelled",
                       "Hello,\n" +
                       serviceProvider.FirstName + " " + serviceProvider.LastName + "\n\n" +
                       userName + " has cancelled service\n" +
                       "Service ID: " + cancelServiceViewModel.ServiceId
                       );
                    }

                    serviceRequest.Comments = cancelServiceViewModel.Comments;
                    serviceRequest.Status = new GlobalData().SERVICE_REQUEST_STATUS_CANCELLED;
                    serviceRequest.ModifiedDate = DateTime.Now;
                    _helperlandContext.ServiceRequests.Update(serviceRequest);
                    _helperlandContext.SaveChanges();
                }
                else
                {
                    return "No service found by that id";
                }

            }
            else
            {
                return "Something is missing";
            }

            return "true";
        }


        public string RateService(RateServiceViewModel rateServiceViewModel)
        {
            if (ModelState.IsValid)
            {
                int n = _helperlandContext.Ratings.Count(u => u.ServiceRequestId == rateServiceViewModel.ServiceId);
                if (n == 0)
                {
                    Rating rating = new Rating();
                    rating.ServiceRequestId = rateServiceViewModel.ServiceId;
                    rating.RatingFrom = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);
                    rating.RatingTo = rateServiceViewModel.ServiceProviderId;
                    rating.Ratings = (int)rateServiceViewModel.Average;
                    rating.Comments = rateServiceViewModel.Comments;
                    rating.RatingDate = DateTime.Now;
                    rating.OnTimeArrival = rateServiceViewModel.OnTime;
                    rating.Friendly = rateServiceViewModel.Friendly;
                    rating.QualityOfService = rateServiceViewModel.QualityOfService;

                    _helperlandContext.Ratings.Add(rating);
                    int noOfRow = _helperlandContext.SaveChanges();

                    if (noOfRow < 1)
                    {
                        return "Something is wrong";
                    }
                }
                else
                {
                    return "You have already submitted a review";
                }
            }
            else
            {
                return "Something is missing";
            }
            return "true";
        }

        public string CustomerDetails(string s)
        {
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            var user = _helperlandContext.Users.Where(u => u.UserId == userId).Select(u => new { u.FirstName, u.LastName, u.Email, u.Mobile, u.DateOfBirth });

            return JsonSerializer.Serialize(user);
        }

        public string UpdateCustomerDetails(UpdateCustomerDetailsViewModel updateCustomerDetailsViewModel)
        {

            if (ModelState.IsValid)
            {
                int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

                User? user = _helperlandContext.Users.Find(userId);

                if (user != null)
                {
                    user.FirstName = updateCustomerDetailsViewModel.FirstName;
                    user.LastName = updateCustomerDetailsViewModel.LastName;
                    user.Mobile = updateCustomerDetailsViewModel.Mobile;
                    user.DateOfBirth = DateTime.ParseExact(updateCustomerDetailsViewModel.DateOfBirth, "yyyy-MM-dd", null);

                    _helperlandContext.Users.Update(user);
                    _helperlandContext.SaveChanges();
                }
                else
                {
                    return "User not found";
                }
            }
            else
            {
                return "Something is missing";
            }
            return "true";
        }

        public string CustomerAddresses(string s)
        {
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            return JsonSerializer.Serialize(_helperlandContext.UserAddresses.Where(u => u.UserId == userId && u.IsDeleted == false).ToList());
        }

        public string RemoveAddres(int AddressId)
        {

            var address = _helperlandContext.UserAddresses.Where(u => u.AddressId == AddressId).FirstOrDefault();
            if (address != null)
            {
                address.IsDeleted = true;
                _helperlandContext.UserAddresses.Update(address);
                _helperlandContext.SaveChanges();
            }
            else
            {
                return "Address not found";
            }
            return "true";
        }

        public string UpdateEditAddress(UpdateEditAddressViewModel updateEditAddressViewModel)
        {
            if (ModelState.IsValid)
            {

                var address = _helperlandContext.UserAddresses.Where(u => u.AddressId == updateEditAddressViewModel.AddressId).FirstOrDefault();

                if (address != null)
                {

                    address.AddressLine1 = updateEditAddressViewModel.AddressLine1;
                    address.AddressLine2 = updateEditAddressViewModel.AddressLine2;
                    address.City = updateEditAddressViewModel.City;
                    address.Mobile = updateEditAddressViewModel.Mobile;

                    _helperlandContext.UserAddresses.Update(address);
                    _helperlandContext.SaveChanges();
                }
                else
                {
                    return "Address not found";
                }
            }
            else
            {
                return "Something is missing";
            }
            return "true";
        }

        public string ChnagePassword(string OldPassword, string NewPassword){
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);
            
            var user = _helperlandContext.Users.Where(u => u.UserId == userId).FirstOrDefault();

            if(user != null){
                string oldPassMd5 = new MD5Hashing().GetMd5Hash(md5Hash,OldPassword);

                if(oldPassMd5.Equals(user.Password)){
                    string newPassHash = new MD5Hashing().GetMd5Hash(md5Hash,NewPassword);
                    user.Password = newPassHash;
                    _helperlandContext.Users.Update(user);
                    _helperlandContext.SaveChanges();
                }
                else{
                    return "Incorrect old password";
                }

            }
            else{
                return "Something is wrong";
            }
            return "true";
        }

        public string GetFevouriteBlockedSPList(string s){
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            var x = (from fb in _helperlandContext.FavoriteAndBlockeds
                     join u in _helperlandContext.Users on fb.TargetUserId equals u.UserId
                     where fb.UserId == userId 
                     select new{
                         SpId = fb.TargetUserId,
                         FirstName = u.FirstName,
                         LastName = u.LastName,

                         Ratings = _helperlandContext.Ratings.Where(u => u.RatingTo == fb.TargetUserId).Select(u => u.Ratings).ToList(),

                         IsFavorite = fb.IsFavorite,
                         IsBlocked = fb.IsBlocked
                     }).ToList();
            return JsonSerializer.Serialize(x);
        }

        public string UpdateFevouriteSP(int TargetUserId, bool IsFavorite){
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);

            FavoriteAndBlocked favoriteAndBlocked = _helperlandContext.FavoriteAndBlockeds.Where(u => u.UserId == userId && u.TargetUserId == TargetUserId).FirstOrDefault();
            if(favoriteAndBlocked != null){
                favoriteAndBlocked.IsFavorite = IsFavorite;
                _helperlandContext.FavoriteAndBlockeds.Update(favoriteAndBlocked);
                _helperlandContext.SaveChanges();
            }
            else{
                return "Target user not found";
            }

            return "true";
        }

        public string UpdateBlockedSP(int TargetUserId, bool IsBlocked){
            int userId = new CurrentLoggedInUser().GetUserId(Request.Cookies["keepMeLoggedInToken"]);
            
            FavoriteAndBlocked favoriteAndBlocked = _helperlandContext.FavoriteAndBlockeds.Where(u => u.UserId == userId && u.TargetUserId == TargetUserId).FirstOrDefault();
            if(favoriteAndBlocked != null){
                favoriteAndBlocked.IsBlocked = IsBlocked;
                _helperlandContext.FavoriteAndBlockeds.Update(favoriteAndBlocked);
                _helperlandContext.SaveChanges();
            }
            else{
                return "Target user not found";
            }
            return "true";
        }
    }
}
