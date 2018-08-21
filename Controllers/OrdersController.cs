﻿using AccordionFair.Data;
using AccordionFair.Data.Entities;
using AccordionFair.Services;
using AccordionFair.ViewModels;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AccordionFair.Controllers
{
    [Route("api/[Controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class OrdersController : Controller
    {
        private readonly IAccordionRepository repo;
        private readonly ILogger<OrdersController> logger;
        private readonly IMapper mapper;
        private readonly UserManager<StoreUser> userManager;
        private readonly IHubContext<NotifyHub> hub;
        private readonly RoleManager<IdentityRole> roleManager;

        public OrdersController(IAccordionRepository repo, 
            ILogger<OrdersController> logger,
            IMapper mapper,
            UserManager<StoreUser> userManager,
            IHubContext<NotifyHub> hub,
            RoleManager<IdentityRole> roleManager)
        {
            this.repo = repo;
            this.logger = logger;
            this.mapper = mapper;
            this.userManager = userManager;
            this.hub = hub;
            this.roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            // var order = repo.GetOrderById(User.Identity.Name, orderId);
            var user2 = User.Identity.Name;
            var user = await userManager.FindByNameAsync(User.Identity.Name);
            // var user = await userManager.GetUserAsync(HttpContext.User);
            

            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains("Admin")) // if user is admin, return all orders
            {
                // vrati sve ordere, bolji handler nedaj boze belaja
                var orders = repo.GetAllOrders();

                if (orders.Any())
                {
                    // kod pisanja koda fora je da cu jednom svoj fokus posvetit odredjenoj metodi i problemu
                    // s obzirom na to bolje je napisat kod tako da, ako u buducnosti se nesto lose desi 
                    // moj kod odmah uputi na moguci problem, ili na to da s njim nema problema

                    // nemoj bit lijen, take your time, pisi kod defenzivno
                    //  mozda je jos rano od mene ocekivat takav kod, ali bih sigurno trebao krenut mislit u tom smijeru

                    logger.LogInformation("Returned all orders for admin account"); 
                    return Ok(orders);
                }
                else
                {
                    logger.LogError("All orders could not be retrieved");
                    return BadRequest();
                }
            }
            else  // if user is not admin, return specific orders
            {
                var orders = repo.GetAllOrdersByUser(username: user.UserName, includeItems: true);

                if (orders.Any())
                {
                    logger.LogInformation($"Returned all orders for {user.UserName} user");
                    return Ok(orders);
                }
                else
                {
                    logger.LogError($"Orders for {user.UserName} could not be retrieved");
                    return BadRequest();
                }
            }
        }


        //[HttpGet]
        //public IActionResult Get(bool includeItems = true)
        //{
        //    try
        //    {
        //        var username = User.Identity.Name;

        //        //var results = repo.GetAllOrders(includeItems);
        //        var results = repo.GetAllOrdersByUser(username, includeItems);

        //        return Ok(mapper.Map<IEnumerable<Order>, IEnumerable<OrderViewModel>>(results));
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError($"Failed to get orders: {ex}");
        //        return BadRequest("Failed to get orders");
        //    }
        //}

        [HttpGet("{id:int}")]
        public IActionResult Get(int id)
        {
            try
            {
                var order = repo.GetOrderById(User.Identity.Name, id);

                if (order != null)
                    return Ok(mapper.Map<Order, OrderViewModel>(order));
                else
                    return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to retrieve order with id: {id}, exception: {ex}");
                return BadRequest($"Failed to retrieve order with id: {id}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]OrderViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var newOrder = mapper.Map<OrderViewModel, Order>(model);

                    if (newOrder.OrderDate == DateTime.MinValue)
                    {
                        newOrder.OrderDate = DateTime.Now;
                    }

                    var currentUser = await userManager.FindByNameAsync(User.Identity.Name);
                    newOrder.User = currentUser;

                    // if (newOrder.BitcoinPrice != model.BtcPrice || newOrder.BitcoinPrice == 0)
                    if (newOrder.BitcoinPrice == 0)
                    {
                        logger.LogError($"BtcPrice is not correctly set modelPrice: {model.BtcPrice} newOrderPrice: {newOrder.BitcoinPrice}");

                        throw new Exception($"BtcPrice is not correctly set modelPrice: {model.BtcPrice} newOrderPrice: {newOrder.BitcoinPrice}");
                    }

                    var subtotal = newOrder.Items.Sum(i => i.Quantity * i.UnitPrice);
                    newOrder.OrderTotalInUSD = subtotal;
                    newOrder.OrderTotalInBitcoin = subtotal / newOrder.BitcoinPrice;
                    //=================================================================================================================
                    RPCCredentialString cred = new RPCCredentialString();
                    cred.UserPassword = new NetworkCredential("marko", "nekadugasifra");
                    RPCClient client = new RPCClient(cred, Network.TestNet);
                    var address = await client.GetNewAddressAsync();
                    newOrder.BitcoinAddress = address.ToString();
                    //=================================================================================================================

                    repo.AddOrder(newOrder);
                    if (repo.SaveAll())
                    {
                        // kad udje, nece imat id, al ce mu kontext dodjelit id nakon Add()
                         return Created($"/api/orders/{newOrder.Id}", mapper.Map<Order, OrderViewModel>(newOrder)); // HTTP response 201 when you create a new object
                        

                        // I get nullreferenceEx  za BitcoinAddress in SHowOrderAddress.cshtml
                       // return RedirectToAction("ShowOrderAddress", "OrderAddress", newOrder.OrderNumber);
                        // redirect to action order address page, 
                    }
                }
                else
                {
                    return BadRequest(ModelState);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to save new order: {ex}");
                return BadRequest($"Failed to save new order prvi");
            }

            return BadRequest("Failed to save new order drugi");
        }
    }
}
