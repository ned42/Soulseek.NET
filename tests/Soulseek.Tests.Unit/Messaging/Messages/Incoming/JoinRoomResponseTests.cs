﻿// <copyright file="JoinRoomResponseTests.cs" company="JP Dillingham">
//     Copyright (c) JP Dillingham. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
//     of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace Soulseek.Tests.Unit.Messaging.Messages
{
    using System.Linq;
    using AutoFixture.Xunit2;
    using Soulseek.Exceptions;
    using Soulseek.Messaging;
    using Soulseek.Messaging.Messages;
    using Xunit;

    public class JoinRoomResponseTests
    {
        [Trait("Category", "Parse")]
        [Fact(DisplayName = "Parse throws MessageExcepton on code mismatch")]
        public void Parse_Throws_MessageException_On_Code_Mismatch()
        {
            var msg = new MessageBuilder()
                .WriteCode(MessageCode.Server.LeaveRoom)
                .Build();

            var ex = Record.Exception(() => JoinRoomResponse.FromByteArray(msg));

            Assert.NotNull(ex);
            Assert.IsType<MessageException>(ex);
        }

        [Trait("Category", "Parse")]
        [Fact(DisplayName = "Parse throws MessageReadException on missing data")]
        public void Parse_Throws_MessageReadException_On_Missing_Data()
        {
            var msg = new MessageBuilder()
                .WriteCode(MessageCode.Server.JoinRoom)
                .WriteInteger(1)
                .Build();

            var ex = Record.Exception(() => JoinRoomResponse.FromByteArray(msg));

            Assert.NotNull(ex);
            Assert.IsType<MessageReadException>(ex);
        }

        [Trait("Category", "Parse")]
        [Theory(DisplayName = "Parse returns expected data when 0 users"), AutoData]
        public void Parse_Returns_Expected_Data_When_0_Users(string roomName)
        {
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.JoinRoom)
                .WriteString(roomName)
                .WriteInteger(0) // user count
                .WriteInteger(0) // status count
                .WriteInteger(0) // data count
                .WriteInteger(0) // slots free count
                .WriteInteger(0); // country count

            var response = JoinRoomResponse.FromByteArray(builder.Build());

            Assert.Equal(roomName, response.RoomName);
            Assert.Equal(0, response.UserCount);
        }

        [Trait("Category", "Parse")]
        [Theory(DisplayName = "Parse returns expected data when 1 user"), AutoData]
        public void Parse_Returns_Expected_Data_When_1_User(string roomName)
        {
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.JoinRoom)
                .WriteString(roomName)
                .WriteInteger(1) // user count
                .WriteString("1")
                .WriteInteger(1) // status count
                .WriteInteger((int)UserStatus.Online)
                .WriteInteger(1) // data count
                .WriteInteger(10) // average speed
                .WriteLong(11) // download count
                .WriteInteger(12) // file count
                .WriteInteger(13) // directory count
                .WriteInteger(1) // slots free count
                .WriteInteger(14) // slots free
                .WriteInteger(1) // country count
                .WriteString("US");

            var res = JoinRoomResponse.FromByteArray(builder.Build());
            var users = res.Users.ToList();

            Assert.Equal(roomName, res.RoomName);
            Assert.Equal(1, res.UserCount);
            Assert.Equal("1", users[0].Username);
            Assert.Equal(UserStatus.Online, users[0].Data.Status);
            Assert.Equal(10, users[0].Data.AverageSpeed);
            Assert.Equal(11, users[0].Data.DownloadCount);
            Assert.Equal(12, users[0].Data.FileCount);
            Assert.Equal(13, users[0].Data.DirectoryCount);
            Assert.Equal(14, users[0].Data.SlotsFree);
            Assert.Equal("US", users[0].Data.CountryCode);
        }

        [Trait("Category", "Parse")]
        [Theory(DisplayName = "Parse returns expected data when 2 users"), AutoData]
        public void Parse_Returns_Expected_Data_When_2_Users(string roomName)
        {
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.JoinRoom)
                .WriteString(roomName)
                .WriteInteger(2) // user count
                .WriteString("10") // user name
                .WriteString("20")
                .WriteInteger(2) // status count
                .WriteInteger((int)UserStatus.Online)
                .WriteInteger((int)UserStatus.Away)
                .WriteInteger(2) // data count
                .WriteInteger(11) // average speed
                .WriteLong(12) // download count
                .WriteInteger(13) // file count
                .WriteInteger(14) // directory count
                .WriteInteger(21) // average speed
                .WriteLong(22) // download count
                .WriteInteger(23) // file count
                .WriteInteger(24) // directory count
                .WriteInteger(2) // slots free count
                .WriteInteger(15) // slots free
                .WriteInteger(25)
                .WriteInteger(2) // country count
                .WriteString("US")
                .WriteString("EN");

            var res = JoinRoomResponse.FromByteArray(builder.Build());
            var users = res.Users.ToList();

            Assert.Equal(roomName, res.RoomName);
            Assert.Equal(2, res.UserCount);
            Assert.Equal("10", users[0].Username);
            Assert.Equal(11, users[0].Data.AverageSpeed);
            Assert.Equal(12, users[0].Data.DownloadCount);
            Assert.Equal(13, users[0].Data.FileCount);
            Assert.Equal(14, users[0].Data.DirectoryCount);
            Assert.Equal(15, users[0].Data.SlotsFree);
            Assert.Equal("US", users[0].Data.CountryCode);
        }

        [Trait("Category", "Parse")]
        [Theory(DisplayName = "Parse returns expected data when private channel"), AutoData]
        public void Parse_Returns_Expected_Data_When_Private_Channel(string roomName)
        {
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.JoinRoom)
                .WriteString(roomName)
                .WriteInteger(1) // user count
                .WriteString("1")
                .WriteInteger(1) // status count
                .WriteInteger((int)UserStatus.Online)
                .WriteInteger(1) // data count
                .WriteInteger(10) // average speed
                .WriteLong(11) // download count
                .WriteInteger(12) // file count
                .WriteInteger(13) // directory count
                .WriteInteger(1) // slots free count
                .WriteInteger(14) // slots free
                .WriteInteger(1) // country count
                .WriteString("US")
                .WriteString("owner")
                .WriteInteger(2) // operator count
                .WriteString("op1")
                .WriteString("op2");

            var res = JoinRoomResponse.FromByteArray(builder.Build());
            var users = res.Users.ToList();

            Assert.Equal(roomName, res.RoomName);
            Assert.Equal(1, res.UserCount);
            Assert.Equal("1", users[0].Username);
            Assert.Equal(UserStatus.Online, users[0].Data.Status);
            Assert.Equal(10, users[0].Data.AverageSpeed);
            Assert.Equal(11, users[0].Data.DownloadCount);
            Assert.Equal(12, users[0].Data.FileCount);
            Assert.Equal(13, users[0].Data.DirectoryCount);
            Assert.Equal(14, users[0].Data.SlotsFree);
            Assert.Equal("US", users[0].Data.CountryCode);
            Assert.True(res.IsPrivateRoom);
            Assert.Equal("owner", res.Owner);
            Assert.Equal(2, res.OperatorCount);
            Assert.Equal("op1", res.Operators.ToList()[0]);
            Assert.Equal("op2", res.Operators.ToList()[1]);
        }
    }
}