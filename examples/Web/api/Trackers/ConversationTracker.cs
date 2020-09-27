﻿namespace WebAPI.Trackers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using WebAPI.Entities;

    /// <summary>
    ///     Tracks private message conversations.
    /// </summary>
    public class ConversationTracker : IConversationTracker
    {
        /// <summary>
        ///     Tracked private message conversations.
        /// </summary>
        public ConcurrentDictionary<string, IList<PrivateMessage>> Conversations { get; } = new ConcurrentDictionary<string, IList<PrivateMessage>>();

        /// <summary>
        ///     Adds a private message conversation and appends the specified <paramref name="message"/>, or just appends the message if the conversation exists.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        public void AddOrUpdate(string username, PrivateMessage message)
        {
            Conversations.AddOrUpdate(username, new List<PrivateMessage>() { message }, (key, value) =>
            {
                value.Add(message);
                return value;
            });
        }

        /// <summary>
        ///     Removes a tracked private message conversation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        public void TryRemove(string username) => Conversations.TryRemove(username, out _);
    }
}
