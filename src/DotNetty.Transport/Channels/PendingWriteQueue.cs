﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A queue of write operations which are pending for later execution. It also updates the writability of the
    /// associated <see cref="IChannel"/> (<see cref="IChannel.IsWritable"/>), so that the pending write operations are
    /// also considered to determine the writability.
    /// </summary>
    public sealed class PendingWriteQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PendingWriteQueue>();

        readonly IChannelHandlerContext _ctx;
        readonly PendingBytesTracker _tracker;

        // head and tail pointers for the linked-list structure. If empty head and tail are null.
        PendingWrite _head;
        PendingWrite _tail;
        int _size;
        long _bytes;

        public PendingWriteQueue(IChannelHandlerContext ctx)
        {
            if (ctx is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }

            _tracker = PendingBytesTracker.NewTracker(ctx.Channel);
            _ctx = ctx;
        }

        /// <summary>
        /// Returns <c>true</c> if there are no pending write operations left in this queue.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                Debug.Assert(_ctx.Executor.InEventLoop);

                return _head is null;
            }
        }

        /// <summary>
        /// Returns the number of pending write operations.
        /// </summary>
        public int Size
        {
            get
            {
                Debug.Assert(_ctx.Executor.InEventLoop);

                return _size;
            }
        }

        /// <summary>
        /// Returns the total number of bytes that are pending because of pending messages. This is only an estimate so
        /// it should only be treated as a hint.
        /// </summary>
        public long Bytes
        {
            get
            {
                Debug.Assert(_ctx.Executor.InEventLoop);

                return _bytes;
            }
        }

        /// <summary>
        /// Adds the given message to this <see cref="PendingWriteQueue"/>.
        /// </summary>
        /// <param name="msg">The message to add to the <see cref="PendingWriteQueue"/>.</param>
        /// <param name="promise"></param>
        public void Add(object msg, IPromise promise)
        {
            Debug.Assert(_ctx.Executor.InEventLoop);
            if (msg is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }
            if (promise is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.promise); }

            // It is possible for writes to be triggered from removeAndFailAll(). To preserve ordering,
            // we should add them to the queue and let removeAndFailAll() fail them later.
            int messageSize = _tracker.Size(msg);
            if (messageSize < 0)
            {
                // Size may be unknow so just use 0
                messageSize = 0;
            }

            PendingWrite write = PendingWrite.NewInstance(msg, messageSize, promise);
            PendingWrite currentTail = _tail;
            if (currentTail is null)
            {
                _tail = _head = write;
            }
            else
            {
                currentTail.Next = write;
                _tail = write;
            }
            _size++;
            _bytes += messageSize;
            _tracker.IncrementPendingOutboundBytes(write.Size);
        }

        /// <summary>
        /// Removes all pending write operations, and fail them with the given <see cref="Exception"/>. The messages
        /// will be released via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <param name="cause">The <see cref="Exception"/> to fail with.</param>
        public void RemoveAndFailAll(Exception cause)
        {
            Debug.Assert(_ctx.Executor.InEventLoop);
            if (cause is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            // It is possible for some of the failed promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to clean them up until the queue is empty.
            for (PendingWrite write = _head; write is object; write = _head)
            {
                _head = _tail = null;
                _size = 0;
                _bytes = 0;
                while (write is object)
                {
                    PendingWrite next = write.Next;
                    ReferenceCountUtil.SafeRelease(write.Msg);
                    IPromise promise = write.Promise;
                    Recycle(write, false);
                    Util.SafeSetFailure(promise, cause, Logger);
                    write = next;
                }
            }
            AssertEmpty();
        }

        /// <summary>
        /// Remove a pending write operation and fail it with the given <see cref="Exception"/>. The message will be
        /// released via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <param name="cause">The <see cref="Exception"/> to fail with.</param>
        public void RemoveAndFail(Exception cause)
        {
            Debug.Assert(_ctx.Executor.InEventLoop);
            if (cause is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            PendingWrite write = _head;

            if (write is null)
            {
                return;
            }
            ReferenceCountUtil.SafeRelease(write.Msg);
            IPromise promise = write.Promise;
            Util.SafeSetFailure(promise, cause, Logger);
            Recycle(write, true);
        }

        /// <summary>
        /// Removes all pending write operation and performs them via <see cref="IChannelHandlerContext.WriteAsync(object, IPromise)"/>
        /// </summary>
        /// <returns>An await-able task.</returns>
        public Task RemoveAndWriteAllAsync()
        {
            Debug.Assert(_ctx.Executor.InEventLoop);

            if (IsEmpty) { return TaskUtil.Completed; }

            // Guard against re-entrance by directly reset
            int currentSize = _size;
            var tasks = new List<Task>(currentSize);

            // It is possible for some of the written promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to write them up until the queue is empty.
            for (PendingWrite write = _head; write is object; write = _head)
            {
                _head = _tail = null;
                _size = 0;
                _bytes = 0;

                while (write is object)
                {
                    PendingWrite next = write.Next;
                    object msg = write.Msg;
                    IPromise promise = write.Promise;
                    Recycle(write, false);
                    if (!promise.IsVoid) { tasks.Add(promise.Task); }
                    _ = _ctx.WriteAsync(msg, promise);
                    write = next;
                }
            }
            AssertEmpty();
            return Task.WhenAll(tasks);
        }

        [Conditional("DEBUG")]
        void AssertEmpty() => Debug.Assert(_tail is null && _head is null && _size == 0);

        /// <summary>
        /// Removes a pending write operation and performs it via <see cref="IChannelHandlerContext.WriteAsync(object, IPromise)"/>.
        /// </summary>
        /// <returns>An await-able task.</returns>
        public Task RemoveAndWriteAsync()
        {
            Debug.Assert(_ctx.Executor.InEventLoop);

            PendingWrite write = _head;
            if (write is null)
            {
                return null;
            }
            object msg = write.Msg;
            IPromise promise = write.Promise;
            Recycle(write, true);
            return _ctx.WriteAsync(msg, promise);
        }

        /// <summary>
        /// Removes a pending write operation and releases it's message via
        /// <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="IPromise" /> of the pending write, or <c>null</c> if the queue is empty.
        /// </returns>
        public IPromise Remove()
        {
            Debug.Assert(_ctx.Executor.InEventLoop);

            PendingWrite write = _head;
            if (write is null)
            {
                return null;
            }
            IPromise promise = write.Promise;
            ReferenceCountUtil.SafeRelease(write.Msg);
            Recycle(write, true);
            return promise;
        }

        /// <summary>
        /// Return the current message, or <c>null</c> if the queue is empty.
        /// </summary>
        public object Current
        {
            get
            {
                Debug.Assert(_ctx.Executor.InEventLoop);

                return _head?.Msg;
            }
        }

        void Recycle(PendingWrite write, bool update)
        {
            PendingWrite next = write.Next;
            long writeSize = write.Size;

            if (update)
            {
                if (next is null)
                {
                    // Handled last PendingWrite so rest head and tail
                    // Guard against re-entrance by directly reset
                    _head = _tail = null;
                    _size = 0;
                    _bytes = 0;
                }
                else
                {
                    _head = next;
                    _size--;
                    _bytes -= writeSize;
                    Debug.Assert(_size > 0 && _bytes >= 0);
                }
            }

            write.Recycle();
            _tracker.DecrementPendingOutboundBytes(writeSize);
        }

        /// <summary>
        /// Holds all meta-data and constructs the linked-list structure.
        /// </summary>
        sealed class PendingWrite
        {
            static readonly ThreadLocalPool<PendingWrite> Pool = new ThreadLocalPool<PendingWrite>(handle => new PendingWrite(handle));

            readonly ThreadLocalPool.Handle _handle;
            public PendingWrite Next;
            public long Size;
            public IPromise Promise;
            public object Msg;

            PendingWrite(ThreadLocalPool.Handle handle)
            {
                _handle = handle;
            }

            public static PendingWrite NewInstance(object msg, int size, IPromise promise)
            {
                PendingWrite write = Pool.Take();
                write.Size = size;
                write.Msg = msg;
                write.Promise = promise;
                return write;
            }

            public void Recycle()
            {
                Size = 0;
                Next = null;
                Msg = null;
                Promise = null;
                _handle.Release(this);
            }
        }
    }
}