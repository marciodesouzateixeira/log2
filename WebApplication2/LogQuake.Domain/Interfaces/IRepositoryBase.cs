﻿using LogQuake.Infra.CrossCuting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogQuake.Domain.Interfaces
{
    public interface IRepositoryBase<TEntity> where TEntity : class
    {
        void Add(TEntity obj);

        TEntity GetById(int Id);

        IEnumerable<TEntity> GetAll(PageRequestBase pageRequest);

        void Update(TEntity obj);

        void Remove(TEntity obj);

        void Dispose();
    }
}