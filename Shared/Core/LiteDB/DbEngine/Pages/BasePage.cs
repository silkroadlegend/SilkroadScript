﻿using System;

namespace LiteDB
{
    internal enum PageType
    {
        Empty = 0,
        Header = 1,
        Collection = 2,
        Index = 3,
        Data = 4,
        Extend = 5
    }

    internal abstract class BasePage
    {
        public BasePage(uint pageID)
        {
            PageID = pageID;
            PrevPageID = uint.MaxValue;
            NextPageID = uint.MaxValue;
            ItemCount = 0;
            FreeBytes = PAGE_AVAILABLE_BYTES;
            DiskData = new byte[0];
        }

        /// <summary>
        ///     Represent page number - start in 0 with HeaderPage [4 bytes]
        /// </summary>
        public uint PageID { get; set; }

        /// <summary>
        ///     Indicate the page type [1 byte] - Must be implemented for each page type
        /// </summary>
        public abstract PageType PageType { get; }

        /// <summary>
        ///     Represent the previous page. Used for page-sequences - MaxValue represent that has NO previous page [4 bytes]
        /// </summary>
        public uint PrevPageID { get; set; }

        /// <summary>
        ///     Represent the next page. Used for page-sequences - MaxValue represent that has NO next page [4 bytes]
        /// </summary>
        public uint NextPageID { get; set; }

        /// <summary>
        ///     Used for all pages to count itens inside this page(bytes, nodes, blocks, ...) [2 bytes]
        ///     Its Int32 but writes in UInt16
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        ///     Used to find a free page using only header search [used in FreeList] [2 bytes]
        ///     Its Int32 but writes in UInt16
        ///     Its updated when a page modify content length (add/remove items)
        /// </summary>
        public int FreeBytes { get; set; }

        /// <summary>
        ///     Indicate that this page is dirty (was modified) and must persist when commited [not-persistable]
        /// </summary>
        public bool IsDirty { get; set; }

        /// <summary>
        ///     This is the data when read first from disk - used to journal operations (IDiskService only will use)
        /// </summary>
        public byte[] DiskData { get; set; }

        /// <summary>
        ///     Every page must imeplement this ItemCount + FreeBytes
        ///     Must be called after Items are updates (insert/deletes) to keep variables ItemCount and FreeBytes synced
        /// </summary>
        public abstract void UpdateItemCount();

        /// <summary>
        ///     Returns a size of specified number of pages
        /// </summary>
        /// <param name="pageCount">The page count</param>
        public static long GetSizeOfPages(uint pageCount)
        {
            return checked((long) pageCount*PAGE_SIZE);
        }

        /// <summary>
        ///     Returns a size of specified number of pages
        /// </summary>
        /// <param name="pageCount">The page count</param>
        public static long GetSizeOfPages(int pageCount)
        {
            if (pageCount < 0) throw new ArgumentOutOfRangeException("pageCount", "Could not be less than 0.");

            return GetSizeOfPages((uint) pageCount);
        }

        #region Page Constants

        /// <summary>
        ///     The size of each page in disk - 4096 is NTFS default
        /// </summary>
        public const int PAGE_SIZE = 4096;

        /// <summary>
        ///     This size is used bytes in header pages 17 bytes (+8 reserved to future use) = 25 bytes
        /// </summary>
        public const int PAGE_HEADER_SIZE = 25;

        /// <summary>
        ///     Bytes avaiable to store data removing page header size - 4071 bytes
        /// </summary>
        public const int PAGE_AVAILABLE_BYTES = PAGE_SIZE - PAGE_HEADER_SIZE;

        #endregion Page Constants

        #region Read/Write page

        /// <summary>
        ///     Create a new instance of page based on T type
        /// </summary>
        public static T CreateInstance<T>(uint pageID)
            where T : BasePage
        {
            var type = typeof (T);

            // casting using "as T" #90 / thanks @Skysper
            if (type == typeof (HeaderPage)) return new HeaderPage() as T;
            if (type == typeof (CollectionPage)) return new CollectionPage(pageID) as T;
            if (type == typeof (IndexPage)) return new IndexPage(pageID) as T;
            if (type == typeof (DataPage)) return new DataPage(pageID) as T;
            if (type == typeof (ExtendPage)) return new ExtendPage(pageID) as T;
            if (type == typeof (EmptyPage)) return new EmptyPage(pageID) as T;

            throw new Exception("Invalid base page type T");
        }

        /// <summary>
        ///     Create a new instance of page based on PageType
        /// </summary>
        public static BasePage CreateInstance(uint pageID, PageType pageType)
        {
            switch (pageType)
            {
                case PageType.Header:
                    return new HeaderPage();
                case PageType.Collection:
                    return new CollectionPage(pageID);
                case PageType.Index:
                    return new IndexPage(pageID);
                case PageType.Data:
                    return new DataPage(pageID);
                case PageType.Extend:
                    return new ExtendPage(pageID);
                case PageType.Empty:
                    return new EmptyPage(pageID);
                default:
                    throw new Exception("Invalid pageType");
            }
        }

        /// <summary>
        ///     Read a page with correct instance page object. Checks for pageType
        /// </summary>
        public static BasePage ReadPage(byte[] buffer)
        {
            var reader = new ByteReader(buffer);

            var pageID = reader.ReadUInt32();
            var pageType = (PageType) reader.ReadByte();

            var page = CreateInstance(pageID, pageType);

            page.ReadHeader(reader);
            page.ReadContent(reader);

            page.DiskData = buffer;

            return page;
        }

        /// <summary>
        ///     Write a page to byte array
        /// </summary>
        public byte[] WritePage()
        {
            var writer = new ByteWriter(PAGE_SIZE);

            WriteHeader(writer);

            if (PageType != PageType.Empty)
            {
                WriteContent(writer);
            }

            DiskData = writer.Buffer;

            return writer.Buffer;
        }

        private void ReadHeader(ByteReader reader)
        {
            // first 5 bytes (pageID + pageType) was readed before class create
            // this.PageID
            // this.PageType

            PrevPageID = reader.ReadUInt32();
            NextPageID = reader.ReadUInt32();
            ItemCount = reader.ReadUInt16();
            FreeBytes = reader.ReadUInt16();
            reader.Skip(8); // reserved 8 bytes
        }

        private void WriteHeader(ByteWriter writer)
        {
            writer.Write(PageID);
            writer.Write((byte) PageType);

            writer.Write(PrevPageID);
            writer.Write(NextPageID);
            writer.Write((ushort) ItemCount);
            writer.Write((ushort) FreeBytes);
            writer.Skip(8); // reserved 8 bytes
        }

        protected abstract void ReadContent(ByteReader reader);

        protected abstract void WriteContent(ByteWriter writer);

        #endregion Read/Write page
    }
}