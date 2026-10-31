"use client";
import {configureStore,createSlice,PayloadAction} from "@reduxjs/toolkit";
const session=createSlice({name:"session",initialState:{role:"Buyer" as string},reducers:{setRole:(s,a:PayloadAction<string>)=>{s.role=a.payload}}});
export const {setRole}=session.actions;
export const store=configureStore({reducer:{session:session.reducer}});
export type RootState=ReturnType<typeof store.getState>;