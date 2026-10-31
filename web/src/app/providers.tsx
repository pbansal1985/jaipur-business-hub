"use client";
import {Provider} from "react-redux";
import {store} from "../store";
import {QueryClient,QueryClientProvider} from "@tanstack/react-query";
import {useState} from "react";
export default function Providers({children}:{children:React.ReactNode}){const [client]=useState(()=>new QueryClient({defaultOptions:{queries:{staleTime:30_000,retry:1}}}));return <Provider store={store}><QueryClientProvider client={client}>{children}</QueryClientProvider></Provider>}