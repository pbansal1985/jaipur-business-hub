import type { Metadata } from "next";
import "./globals.css";
import Providers from "./providers";
export const metadata:Metadata={title:{default:"Jaipur Business Hub | Local suppliers, products and services",template:"%s | Jaipur Business Hub"},description:"Find Jaipur and Rajasthan businesses, manufacturers, suppliers and professional services.",openGraph:{type:"website",title:"Jaipur Business Hub",description:"Find trusted businesses across Jaipur and Rajasthan."}};
export default function RootLayout({children}:{children:React.ReactNode}){return <html lang="en-IN"><body><Providers>{children}</Providers></body></html>}